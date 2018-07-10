using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace HL7Helper_DLL
{
    public class Segment
    {
        private Dictionary<int, string> _Fields;

        public Segment()
        {
            _Fields = new Dictionary<int, string>(20);
        }

        public Segment(string _Name)
        {
            _Fields = new Dictionary<int, string>(20);
            _Fields.Add(0, _Name);
        }

        public string Name
        {
            get
            {
                if (!_Fields.ContainsKey(0))
                {
                    return string.Empty;
                }
                return _Fields[0];
            }
            set
            { }
        }

        public string Field(int _Index)
        {
            // This implementation supports only vertical bars as field delimiters
            if (Name == "MSH" && _Index == 1)
            {
                return "|";
            }
            if (!_Fields.ContainsKey(_Index))
            {
                return string.Empty;
            }
            return _Fields[_Index];
        }

        public void Field(int _Index, string _Value)
        {
            // This implementation supports only vertical bars as field delimiters
            if (Name == "MSH" && _Index == 1)
            {
                return;
            }

            if (_Fields.ContainsKey(_Index))
            {
                _Fields.Remove(_Index);
            }

            if (!string.IsNullOrEmpty(_Value))
            {
                _Fields.Add(_Index, _Value);
            }
        }

        public void Parse(string _Text)
        {
            int intCount = 0;
            char[] chrDelimiter = { '|' };

            string strTemp = _Text.Trim('|');
            string[] aryTokens = strTemp.Split(chrDelimiter, StringSplitOptions.None);

            foreach (string strItem in aryTokens)
            {
                Field(intCount, strItem);

                if (strItem == "MSH")
                {
                    //Treat the special case "MSH" - the delimiter after the segment name counts as first field
                    intCount++;
                }
                intCount++;
            }
        }

        public string Serialize()
        {
            int intMax = 0;
            foreach (KeyValuePair<int, string> keyField in _Fields)
            {
                if (intMax < keyField.Key)
                {
                    intMax = keyField.Key;
                }
            }

            StringBuilder sbTemp = new StringBuilder();
            for (int intIndex = 0; intIndex <= intMax; intIndex++)
            {
                if (_Fields.ContainsKey(intIndex))
                {
                    sbTemp.Append(_Fields[intIndex]);

                    // Treat special case "MSH" - the first delimiter after segement name counts as first field
                    if (intIndex == 0 && Name.Equals("MSH"))
                    {
                        continue;
                    }
                }

                if (intIndex != intMax)
                {
                    sbTemp.Append("|");
                }
            }
            return sbTemp.ToString();
        }

        public void JsonParse(JObject _Obj, string _Name, DateTime _dtNow, XmlNode _XmlSource)
        {
            XmlNodeList xmlNodeList = _XmlSource.SelectNodes("Element[@Code='" + _Name + "' and @PositionItem='']");
            foreach (XmlElement xmlElement in xmlNodeList)
            {
                string objPosition = xmlElement.GetAttribute("Position");
                string objType = xmlElement.GetAttribute("Type");
                string objOPT = xmlElement.GetAttribute("OPT");
                string objMaxLen = xmlElement.GetAttribute("MaxLen");
                string objDataTag = xmlElement.GetAttribute("DataTag");
                string objValue = xmlElement.GetAttribute("Value");

                XmlNodeList xmlItemList = _XmlSource.SelectNodes("Element[@Code='" + _Name + "' and @Position='" + objPosition + "' and @PositionItem>=0]");
                if (xmlItemList.Count > 0)
                {
                    //Child
                    string strSpan = "";
                    string strValue = "";

                    foreach (XmlElement xmlElementItem in xmlItemList)
                    {
                        string itemPositionItem = xmlElementItem.GetAttribute("PositionItem");
                        string itemOPT = xmlElementItem.GetAttribute("OPT");
                        string itemMaxLen = xmlElementItem.GetAttribute("MaxLen");
                        string itemDataTag = xmlElementItem.GetAttribute("DataTag");
                        string itemValue = xmlElementItem.GetAttribute("Value");

                        if (!string.IsNullOrEmpty(itemDataTag))
                        {
                            var jsonValue = _Obj.SelectToken(itemDataTag);
                            if (jsonValue != null)
                            {
                                itemValue = jsonValue.ToString();
                            }
                        }
                        if (!string.IsNullOrEmpty(objValue) && !string.IsNullOrEmpty(itemMaxLen))
                        {
                            int maxLen;
                            int.TryParse(itemMaxLen, out maxLen);

                            if (objValue.Length > maxLen)
                            {
                                objValue = objValue.Substring(0, maxLen);
                            }
                        }

                        strValue = strValue + strSpan + itemValue;
                        strSpan = "^";
                    }

                    objValue = strValue.TrimEnd('^');
                }
                else
                {
                    //Parent
                    if (!string.IsNullOrEmpty(objDataTag))
                    {
                        var jsonValue = _Obj.SelectToken(objDataTag);
                        switch (objType)
                        {
                            case "DTM":
                                if (jsonValue != null)
                                {
                                    if (IsDate(jsonValue.ToString()))
                                    {
                                        if (DateTime.Parse(jsonValue.ToString()).Equals(DateTime.MinValue))
                                        {
                                            objValue = "";
                                        }
                                        else
                                        {
                                            switch (objDataTag)
                                            {
                                                case "BirthDate":
                                                    objValue = DateTime.Parse(jsonValue.ToString()).ToString("yyyyMMdd");
                                                    break;
                                                default:
                                                    objValue = DateTime.Parse(jsonValue.ToString()).ToString("yyyyMMddHHmmss");
                                                    break;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        objValue = "";
                                    }
                                }
                                break;
                            default:
                                if (jsonValue != null)
                                {
                                    objValue = jsonValue.ToString();
                                }
                                break;
                        }
                    }

                    int position = 0;
                    int.TryParse(objPosition, out position);
                    if (position == 2 && _Name.Equals("MSH"))
                    {
                        switch (objOPT)
                        {
                            case "R":
                                if (string.IsNullOrEmpty(objValue) && objType.Equals("TS"))
                                {
                                    objValue = _dtNow.ToString("yyyyMMddHHmmss");
                                }
                                break;

                            default:
                                break;
                        }
                    }

                    if (!string.IsNullOrEmpty(objValue) && !string.IsNullOrEmpty(objMaxLen))
                    {
                        int maxLen = 0;
                        int.TryParse(objMaxLen, out maxLen);
                        if (objValue.Length > maxLen)
                        {
                            objValue = objValue.Substring(0, maxLen);
                        }
                    }
                    if (!string.IsNullOrEmpty(objValue) && !string.IsNullOrEmpty(objValue.Replace("^", "")))
                    {
                        Field(position, objValue);
                    }
                }
            }

        }

        private bool IsDate(string date)
        {
            try
            {
                DateTime dt = DateTime.Parse(date);
                return true;
            }
            catch
            {
                return false;
            }
        }



    }  
}
