using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Com.AugustCellars.CoAP;
using Com.AugustCellars.CoAP.Net;
using Com.AugustCellars.CoAP.ResourceDirectory;
using Com.AugustCellars.CoAP.Server.Resources;

namespace RD.Net46
{
    public class SimpleRegistration : DiscoveryResource
    {
        private EndpointRegister _registerAt;
        public SimpleRegistration(IResource root, EndpointRegister register) : base(root)
        {
            _registerAt = register;
        }

        protected override void DoPost(CoapExchange exchange)
        {
            exchange.Respond(StatusCode.Changed);

            try {
                System.Net.EndPoint ep = exchange.Request.Source;
                IPEndPoint ep2 = ep as IPEndPoint;

                string context;

                if (ep2 != null) {
                    if (ep2.AddressFamily == AddressFamily.InterNetworkV6) {
                        context = $"coap://[{ep2.Address}]:{ep2.Port}";
                    }
                    else {
                        context = $"coap://{ep2.Address}:{ep2.Port}";
                    }
                }
                else {
                    //  Can't do anything with this address
                    return;
                }


                Uri u = new Uri(context);

                CoapClient c = new CoapClient(u);
                c.UriPath = "/.well-known/core";
                c.Timeout = 2000;
                
                Response r = c.Get();
                if (r == null || r.StatusCode != StatusCode.Content) {
                    return;
                }

                _registerAt.RegisterEndpoint(exchange.Request.UriQueries, r.Payload, r.ContentType, r.Source);

            }
            catch (Exception e) {
                Console.WriteLine("Error doing SimpleRegistration::DoPost " + e.ToString());
            }
        }
    }
}
