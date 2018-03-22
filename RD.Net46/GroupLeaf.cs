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
                if (value != null && value[0] == '"') {
                    //  M00BUG check to makes sure we don't need to strip this.
                }

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
                        throw new Exception("Will do a BadOption exception");
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
                if (attributes.Length != 1) return StatusCode.BadRequest;
                EndPointNames.Add(attributes[0]);
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
