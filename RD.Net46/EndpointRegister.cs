using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Com.AugustCellars.CoAP;
using Com.AugustCellars.CoAP.EndPoint.Resources;
using Com.AugustCellars.CoAP.Server.Resources;
using Resource = Com.AugustCellars.CoAP.Server.Resources.Resource;

namespace Com.AugustCellars.CoAP.ResourceDirectory
{
    public class EndpointRegister : Resource
    {
        static Random _Random = new Random();
        private static string _CharSet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        /// <summary>
        /// Resource corresponding to a resource directory resource.
        /// </summary>
        /// <param name="resourceName"></param>
        public EndpointRegister(string resourceName) : base(resourceName)
        {
            Attributes.AddResourceType("core.rd");
            Attributes.AddContentType(MediaType.ApplicationLinkFormat);
            Attributes.AddContentType(9999); // TODO - Add application/link-format+json and application/link-format+cbor
        }

        public string DefaultDomain { get; set; }

        internal List<EndpointNode> ChildEndpointNodes { get; } = new List<EndpointNode>();
    

        /// <summary>
        /// Process POST messages
        /// </summary>
        /// <param name="exchange"></param>
        protected override void DoPost(CoapExchange exchange)
        {
            Request req = exchange.Request;

            if (req.PayloadSize == 0) {
                exchange.Respond(StatusCode.BadRequest);
                return;
            }

            try {

#if true
                RemoteResource links = RemoteResource.NewRoot(req.PayloadString);
#else
            RemoteResource links = RemoteResource.NewRoot(req.Payload, req.ContentType);
#endif

                string epName = null;
                string d = null;
                foreach (string q in req.UriQueries) {
                    if (q.StartsWith("eq=")) epName = q.Substring(3);
                    else if (q.StartsWith("d=")) d = q.Substring(2);
                }
                if (d == null && DefaultDomain != null) d = DefaultDomain;

                string childName = null;

                foreach (EndpointNode node in ChildEndpointNodes) {
                    if (node.Domain == d && node.Name == epName) {

                            node.Reload(links, req.UriQueries);
                            Response response = new Response(StatusCode.Changed) {
                                LocationPath = node.Path
                            };
                            exchange.Respond(response);
                        return;
                    }
                }

                //  URI variables - 
                //  ep - endpoint name - required
                //  d - domain - optional
                //  et - endpoint type - optional
                //  con - context - optional
                // 

                    do {
                        childName = NewName();
                    } while (GetChild(childName) != null);
                


                EndpointNode newChild = new EndpointNode(childName, links, req.UriQueries);

                if (newChild.Domain == null) newChild.Domain = DefaultDomain;
                if (newChild.Context == null) {
                    System.Net.EndPoint ep = req.Source;
                    IPEndPoint ep2 = ep as IPEndPoint;
                    ;
                    if (ep2 != null) {
                        newChild.Context = $"coap://{ep2.Address}:{ep2.Port}";
                    }
                }

                foreach (EndpointNode child in ChildEndpointNodes) {
                    if (child.Domain == newChild.Domain && child.EndpointName == newChild.EndpointName) {
                        exchange.Respond(StatusCode.BadOption);
                        return;
                    }
                }

                Add(newChild);
                Response res = Response.CreateResponse(req, StatusCode.Created);

                res.LocationPath = Path + Name + "/" + childName;

                exchange.Respond(res);
            }
            catch {
                exchange.Respond(StatusCode.BadRequest);
            }
        }

        public bool HasEndpoint(string domain, string endpoint)
        {
            return true;
        }

        private string NewName()
        {
            return _CharSet.Select(c => _CharSet[_Random.Next(_CharSet.Length)]).Take(4).ToString();
        }
    }
}
