using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
using Com.AugustCellars.CoAP;
using Com.AugustCellars.CoAP.Server.Resources;
using PeterO.Cbor;

namespace Com.AugustCellars.CoAP.ResourceDirectory
{
    public class EndpointLookup : Resource
    {
        private readonly EndpointRegister _root;

        public EndpointLookup(string name, EndpointRegister root) : base(name)
        {
            _root = root;
            Attributes.AddResourceType("core.rd-lookup-ep");
            Attributes.AddContentType(MediaType.ApplicationLinkFormat);
            Attributes.AddContentType(MediaType.ApplicationCbor);
            Attributes.AddContentType(MediaType.ApplicationJson);

            root.EndpointLookupResource = this;
        }

        /// <summary>
        /// Inherit Doc.
        /// </summary>
        /// <param name="exchange"></param>
        protected override void DoGet(CoapExchange exchange)
        {
            Request req = exchange.Request;
            CBORObject items = null;
            StringBuilder sb = null;
            Dictionary<string, CBORObject> dict = null;
            int retContentType = MediaType.ApplicationLinkFormat;

            try {
                Filter filter = new Filter(req.UriQueries);
                int firstItem = 0;
                int lastItem = int.MaxValue;

                if (filter.Count != int.MaxValue) {
                    firstItem = filter.Count * filter.Page;
                    lastItem = firstItem + filter.Page - 1;
                }
                else if (filter.Page != 0) {
                    exchange.Respond(StatusCode.BadRequest);
                    return;
                }

                Response resp = Response.CreateResponse(req, StatusCode.Content);

                if (req.HasOption(OptionType.Accept)) {
                    foreach (Option acceptOption in req.GetOptions(OptionType.Accept)) {
                        switch (acceptOption.IntValue) {
                        case MediaType.ApplicationLinkFormat:
                            sb = new StringBuilder();
                            break;

                        case MediaType.ApplicationLinkFormatCbor:
                            items = CBORObject.NewArray();
                            dict = LinkFormat.CborAttributeKeys;
                            break;

                        case MediaType.ApplicationLinkFormatJson:
                            items = CBORObject.NewArray();
                            break;

                        default:
                            // Ignore value
                            break;

                        }

                        //  We found a value
                        if (sb != null || items != null) {
                            retContentType = acceptOption.IntValue;
                            break;
                        }
                    }

                    if (sb == null && items == null) {
                        exchange.Respond(StatusCode.NotAcceptable);
                        return;
                    }
                }
                else {
                    sb = new StringBuilder();
                    retContentType = MediaType.ApplicationLinkFormat;
                }

                int itemCount = -1;

                foreach (EndpointNode ep in _root.ChildEndpointNodes) {
                    if (ep.IsDeleted) continue;
                    filter.ClearState();
                    ep.ApplyFilter(filter, true, true);
                    if (filter.Passes) {
                        itemCount += 1;
                        if (itemCount < firstItem || itemCount > lastItem) continue;
                        if (sb != null) {
                            ep.GetLink(sb);
                        }
                        else {
                            ep.GetLink(items, dict);
                        }
                    }
                }

                if (sb != null) {
                    if (sb.Length > 0) sb.Remove(sb.Length - 1, 1);
                    resp.PayloadString = sb.ToString();
                }
                else if (dict == null) {
                    resp.PayloadString = items.ToJSONString();
                }
                else {
                    resp.Payload = items.EncodeToBytes();
                }

                resp.ContentFormat = retContentType;

                exchange.Respond(resp);
            }
            catch {
                exchange.Respond(StatusCode.BadRequest);
            }
        }
    }
}
