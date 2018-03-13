using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Com.AugustCellars.CoAP;
using Com.AugustCellars.CoAP.Server.Resources;
using PeterO.Cbor;

namespace Com.AugustCellars.CoAP.ResourceDirectory
{
    public class GroupLeaf : Resource
    {

        public GroupLeaf(string resourceName, Request req) : base(resourceName)
        {
            foreach (string query in req.UriQueries) {
                string[] items = query.Split('=');
                string key = items[0];
                string value = (items.Length == 1) ? items[1] : null;

                switch (items[0]) {
                    case "gp":
                        GroupName = value;
                        break;

                    case "d":
                        Domain = value;
                        break;

                    case "con":
                        Context = value;
                        break;

                    default:
                        // TODO Make this an error
                        break;
                }

                Attributes.Add(key, value);
            }
        }

        public string Context { get; private set; }
        public string Domain { get; private set; }
        public string GroupName { get; private set; }
        public List<string> EndPointNames { get;  } = new List<string>();

        protected override void DoDelete(CoapExchange exchange)
        {
            Parent.Remove(this);
            exchange.Respond(StatusCode.Deleted);
        }

        public StatusCode UpdateContent(string body)
        {
            string[] lines = body.Split(',');
            foreach (string line in lines) {
                string[] attributes = line.Split(';');
                if (attributes.Length != 2) return StatusCode.BadRequest;
                if (attributes[0] != "<>") return StatusCode.BadRequest;
                attributes = attributes[1].Split('=');
                if (attributes.Length != 2 || attributes[0] != "ep") return StatusCode.BadRequest;
                if (attributes[1][0] == '=') {
                    attributes[1] = attributes[1].Substring(1, attributes[1].Length - 2);
                }
                EndPointNames.Add(attributes[1]);
            }

            return StatusCode.Created;
        }

        internal bool Apply(Filter filter, bool fSearchDown)
        {
            if (filter.Apply(Attributes)) return true;

            if (fSearchDown) {
            }

            return false;
        }

        public void SerializeResource(StringBuilder sb)
        {
            LinkFormat.SerializeResource(this, sb);
        }

        public void SerializeResource(CBORObject root, Dictionary<string, CBORObject> dict)
        {
            LinkFormat.SerializeResource(this, root, dict);
        }
    }
}
