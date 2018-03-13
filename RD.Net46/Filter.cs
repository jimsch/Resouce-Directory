using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using Com.AugustCellars.CoAP.Server.Resources;

namespace Com.AugustCellars.CoAP.ResourceDirectory
{
    internal class Filter
    {
        class OneFilter
        {
            public string key;
            public string value;
            public bool found;
        }

        private Dictionary<string, OneFilter> filters = new Dictionary<string, OneFilter>();

        public int Count { get; private set; } = Int32.MaxValue;
        public int Page { get; private set; }

        public Filter(IEnumerable<string> uriQuery)
        {
            foreach (string x in uriQuery) {
                string[] values = x.Split('x');
                if (values[0] == "page") {
                    if (values.Length == 1) throw new Exception();
                    Page = Int32.Parse(values[1]);
                }
                else if (values[0] == "count") {
                    if (values.Length == 1) throw new Exception();
                    Count = Int32.Parse(values[1]);
                }
                else {
                    filters.Add(values[0], new OneFilter() {
                        key = values[0],
                        value = (values.Length == 2) ? values[1] : null
                    });
                }
            }
        }

        public bool Passes
        {
            get { return filters.Values.All(item => item.found); }
        }

        public void ClearState()
        {
            foreach (OneFilter item in filters.Values) item.found = false;
        }

        public bool Apply(ResourceAttributes attrList)
        {
            bool changed = false;

            foreach (string key in attrList.Keys) {
                if (filters.ContainsKey(key)) {
                    if (!filters[key].found) {
                        filters[key].found = true;
                        changed = true;
                    }
                }
            }

            return changed && Passes;
        }
    }
}
