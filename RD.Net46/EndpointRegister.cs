﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Com.AugustCellars.CoAP.EndPoint.Resources;
using Com.AugustCellars.CoAP.Server.Resources;
using Com.AugustCellars.CoAP.Server;
using Resource = Com.AugustCellars.CoAP.Server.Resources.Resource;

namespace Com.AugustCellars.CoAP.ResourceDirectory
{
    public class EndpointRegister : Resource
    {
        static readonly Random _Random = new Random();
        private static string _CharSet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        public CoapServer Server { get; }

        /// <summary>
        /// Resource corresponding to a resource directory resource.
        /// </summary>
        /// <param name="resourceName"></param>
        public EndpointRegister(string resourceName, CoapServer server) : base(resourceName)
        {
            Attributes.AddResourceType("core.rd");
            Attributes.AddContentType(MediaType.ApplicationLinkFormat);
            Attributes.AddContentType(MediaType.ApplicationLinkFormatCbor);
            Attributes.AddContentType(MediaType.ApplicationLinkFormatJson);
            ETag = new byte[] {0};

            Server = server;
        }

        /// <summary>
        /// Allow for a default domain 
        /// </summary>
        public string DefaultDomain { get; set; }

        internal List<EndpointNode> ChildEndpointNodes { get; } = new List<EndpointNode>();

        /// <summary>
        /// What is the group manager that is associated with this endpoint registar.
        /// </summary>
        public GroupManager GroupMgr { get; set; }
        public GroupLookup GroupLookupResource { get; set; }
        public EndpointLookup EndpointLookupResource { get; set; }
        public ResourceLookup ResourceLookupResource { get; set; }

        // Get the current ETag value
        public byte[] ETag { get; internal set; }

        /// <summary>
        /// Process POST messages
        /// </summary>
        /// <param name="exchange"></param>
        protected override void DoPost(CoapExchange exchange)
        {
            try {
                Request req = exchange.Request;

                if (req.PayloadSize == 0) {
                    exchange.Respond(StatusCode.BadRequest);
                    return;
                }

                Response response = RegisterEndpoint(req.UriQueries, req.Payload, req.ContentFormat, req.Source);
                if (response != null) {
                    exchange.Respond(response);
                }
                else {
                    exchange.Respond(StatusCode.BadRequest);
                }
            }
            catch (Exception) {
                exchange.Respond(StatusCode.InternalServerError);
            }
        }

        public Response RegisterEndpoint(IEnumerable<string> uriQueriesIn, byte[] payload, int contentType, System.Net.EndPoint source)
        { 
            try {
                List<string> uriQueries = uriQueriesIn.ToList();
                //  Parse down the content

                RemoteResource links = RemoteResource.NewRoot(payload, contentType);
                /*
                 I don't think this is relevant any more

                foreach (IResource link in links.Children) {
                    if (!link.Attributes.Contains("base")) {
                        link.Attributes.Add("base", "");
                    }
                }
                */

                //
                //  Look for the required/optional known parameters
                //  * ep is required unless we can infer it
                //  * d is optional but may be inferred

                string epName = null;
                string d = null;
                foreach (string q in uriQueries) {
                    if (q.StartsWith("ep=")) epName = q.Substring(3);
                    else if (q.StartsWith("d=")) d = q.Substring(2);
                }

                if (d == null && DefaultDomain != null) d = DefaultDomain;

                //  Look to see if all we are going to do is to update an existing
                //  registration.
                //  For existing registrations we don't re-write the base URL
                //
                //  Atomic change of the current version, replace with new content

                foreach (EndpointNode node in ChildEndpointNodes) {
                    if (node.Domain == d && node.EndpointName == epName) {

                        node.Reload(links, uriQueries);
                        Response response = new Response(StatusCode.Changed) {
                            LocationPath = node.Uri
                        };
                        return response;
                    }
                }

                //  We need to generate a path name for this node

                string childName;
                do {
                    childName = NewName();
                } while (GetChild(childName) != null);

                //  Create the new end point

                EndpointNode newChild = new EndpointNode(childName, links, uriQueries, this);

                if (newChild.Domain == null) newChild.Domain = DefaultDomain;

                if (newChild.BaseUrl == null) {
                    System.Net.EndPoint ep = source;

                    if (ep is IPEndPoint ep2) {
                        if (ep2.AddressFamily == AddressFamily.InterNetworkV6) {
                            newChild.BaseUrl = new Uri($"coap://[{ep2.Address}]:{ep2.Port}");
                        }
                        else if (ep2.AddressFamily == AddressFamily.InterNetwork) {
                            newChild.BaseUrl = new Uri($"coap://{ep2.Address}:{ep2.Port}");
                        }
                        else {
                            throw new Exception("Unknown address family");
                        }
                        //  Should not be necessary because we always go with absolute addressing.
                        // newChild.RegistrationAttributes.Add("anchor", newChild.BaseUrl);
                    }
                }

                ChildEndpointNodes.Add(newChild);
                Add(newChild);
                Response res = new Response(StatusCode.Created) {
                    LocationPath = Path + Name + "/" + childName
                };


                //  Propagate the content change through the system
                //  M00TODO - uncomment this
                //  ContentChanged();
                return res;
            }
            catch {
                return new Response(StatusCode.BadRequest);
            }
        }

        public bool HasEndpoint(string domain, string endpoint)
        {
            return true;
        }

        private static string NewName()
        {
            /*
            return _CharSet.Select(c => _CharSet[_Random.Next(_CharSet.Length)]).Take(4).ToString();
            */
            string str = "";
            for (int i = 0; i < 4; i++) {
                str += _CharSet[_Random.Next(_CharSet.Length)];
            }

            return str;
        }

        public void ContentChanged()
        {
            // Increment the ETag
            bool carry = false;
            for (int i = 0; i < ETag.Length; i++) {
                ETag[i] += 1;
                if (ETag[i] == 0 && (i == ETag.Length - 1)) {
                    carry = true;
                }
                else {
                    break;
                }
            }

            if (carry) {
                if (ETag.Length == 4) {
                    ETag = new byte[1];
                }
                else {
                    ETag = new byte[ETag.Length + 1];
                }
            }

            if (GroupLookupResource != null) {
                GroupLookupResource.Changed();
            }

            EndpointLookupResource.Changed();
            ResourceLookupResource.Changed();
        }

        public static void Cleanup(Object obj)
        {
            EndpointRegister epr = (EndpointRegister) obj;
            List<EndpointNode> toDelete = new List<EndpointNode>();

            foreach (EndpointNode ep in epr.ChildEndpointNodes) {
                if (ep.IsDeleted) {
                    toDelete.Add(ep);
                }
            }

            foreach (EndpointNode ep in toDelete) {
                epr.ChildEndpointNodes.Remove(ep);
                foreach (GroupLeaf group in epr.GroupMgr.AllGroups) {
                    group.RemoveEndpoint(ep);
                }
            }

            
        }
    }
}
