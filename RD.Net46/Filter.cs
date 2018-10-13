using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
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
            public bool prefix;
        }

        private Dictionary<string, OneFilter> filters = new Dictionary<string, OneFilter>();

        public int Count { get; private set; } = Int32.MaxValue;
        public int Page { get; private set; }

        public Filter(IEnumerable<string> uriQuery)
        {
            foreach (string x in uriQuery) {
                string[] values = x.Split('=');
                if (values[0] == "page") {
                    if (values.Length == 1) throw new Exception();
                    Page = int.Parse(values[1]);
                }
                else if (values[0] == "count") {
                    if (values.Length == 1) throw new Exception();
                    Count = int.Parse(values[1]);
                }
                else {
                    bool prefix = false;
                    string filterValue = null;
                    if (values.Length == 2) {
                        filterValue = values[1];
                        if (filterValue[0] == '"' && filterValue[filterValue.Length - 1] == '"') {
                            filterValue = filterValue.Substring(1, filterValue.Length - 2);
                        }
                        prefix = filterValue[filterValue.Length - 1] == '*';
                        if (prefix) {
                            filterValue = filterValue.Substring(0, filterValue.Length - 1);
                        }
                    }



                    filters.Add(values[0], new OneFilter() {
                        key = values[0], value = filterValue, prefix = prefix
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

        public bool Href(string value)
        {
            if (filters.ContainsKey("href")) {
                if (!filters["href"].found) {
                    if (filters["href"].prefix) {
                        if (value.Length >= filters["href"].value.Length &&
                            filters["href"].value == value.Substring(0, filters["href"].value.Length)) {
                            filters["href"].found = true;
                            return true;
                        }
                    }
                    else {
                        if (filters["href"].value == value) {
                            filters["href"].found = true;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public bool Apply(ResourceAttributes attrList)
        {
            bool changed = false;

            foreach (string key in attrList.Keys) {
                if (filters.ContainsKey(key)) {
                    if (!filters[key].found) {
                        if (filters[key].value == null) {
                            filters[key].found = true;
                            changed = true;
                        }
                        else {
                            foreach (string v in attrList.GetValues(key)) {
                                if (filters[key].prefix) {
                                    if (v.Length >= filters[key].value.Length &&
                                        filters[key].value == v.Substring(0, filters[key].value.Length)) {
                                        filters[key].found = true;
                                        changed = true;
                                        break;
                                    }
                                }
                                else {
                                    if (filters[key].value == v) {
                                        filters[key].found = true;
                                        changed = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return changed && Passes;
        }
    }
}
