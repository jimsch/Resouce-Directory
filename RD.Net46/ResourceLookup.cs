using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Com.AugustCellars.CoAP;
using Com.AugustCellars.CoAP.Server.Resources;
using PeterO.Cbor;
using Com.AugustCellars.CoAP.ResourceDirectory;

namespace Com.AugustCellars.CoAP.ResourceDirectory
{
    public class ResourceLookup : Resource
    {
        private readonly EndpointRegister _root;

        public ResourceLookup(string name, EndpointRegister root) : base(name)
        {
            _root = root;
            Attributes.AddResourceType("core.rd-lookup-res");
            Attributes.AddContentType(MediaType.ApplicationLinkFormat);
            Attributes.AddContentType(MediaType.ApplicationCbor);
            Attributes.AddContentType(MediaType.ApplicationJson);
        }


        protected override void DoGet(CoapExchange exchange)
        {
            Request req = exchange.Request;
            CBORObject items = null;
            StringBuilder sb = null;
            Dictionary<string, CBORObject> dict = null;

            try {
                Filter filter = new Filter(req.UriQueries);
                int firstItem = 0;
                int lastItem = Int32.MaxValue;

                if (filter.Count != Int32.MaxValue) {
                    firstItem = filter.Count * filter.Page;
                    lastItem = firstItem + filter.Page - 1;
                }

                Response resp = Response.CreateResponse(req, StatusCode.Content);

                if (req.HasOption(OptionType.Accept)) {
                    switch (req.GetFirstOption(OptionType.Accept).IntValue) {
                        case MediaType.ApplicationLinkFormat:
                            sb = new StringBuilder();
                            break;

                        case MediaType.ApplicationCbor:
                            items = CBORObject.NewArray();
                            dict = LinkFormat._CborAttributeKeys;
                            break;

                        case MediaType.ApplicationJson:
                            items = CBORObject.NewArray();
                            break;

                        default:
                            exchange.Respond(StatusCode.BadOption);
                            return;
                    }
                    resp.ContentType = req.GetFirstOption(OptionType.Accept).IntValue;
                }
                else {
                    sb = new StringBuilder();
                }

                int itemCount = -1;

                foreach (EndpointNode ep in _root.ChildEndpointNodes) {
                    foreach (IResource link in ep.Links) {
                        filter.ClearState();
                        if (!filter.Apply(link.Attributes)) {
                            ep.ApplyFilter(filter, true, false);
                        }
                        if (filter.Passes) {
                            itemCount += 1;
                            if (itemCount < firstItem || itemCount > lastItem) continue;
                            if (sb != null) {
                                LinkFormat.SerializeResource(link, sb);
                            }
                            else {
                                LinkFormat.SerializeResource(link, items, dict);
                            }
                        }
                    }
                }

                if (sb != null) {
                    resp.PayloadString = sb.ToString();
                }
                else if (dict == null) {
                    resp.PayloadString = items.ToJSONString();
                }
                else {
                    resp.Payload = items.EncodeToBytes();
                }

                exchange.Respond(resp);
            }
            catch {
                exchange.Respond(StatusCode.BadRequest);
            }
        }
    }
}
