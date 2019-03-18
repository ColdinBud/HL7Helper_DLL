using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace HL7Helper_DLL
{
    public class Message
    {
        private const string MSH = "MSH";
        private const int MSH_MSG_TIME = 7;
        private const int MSH_MSG_TYPE = 9;
        private const int MSH_MSG_CONTROL_ID = 10;

        private LinkedList<Segment> _Segments;

        public Message()
        {
            Clear();
        }

        public void Clear()
        {
            _Segments = new LinkedList<Segment>();
        }

        protected Segment Header()
        {
            if (_Segments.Count == 0 || !_Segments.First.Value.Name.Equals("MSH"))
            {
                return null;
            }
            return _Segments.First.Value;
        }

        public int Count()
        {
            return _Segments.Count();
        }

        public string Type()
        {
            var segMSH = Header();
            if (segMSH == null)
            {
                return string.Empty;
            }
            return segMSH.Field(MSH_MSG_TYPE);
        }

        public string CtrlID()
        {
            var segMSH = Header();
            if (segMSH == null)
            {
                return string.Empty;
            }
            return segMSH.Field(MSH_MSG_CONTROL_ID);
        }

        public string MessageTime()
        {
            var segMSH = Header();
            if (segMSH == null)
            {
                return string.Empty;
            }

            DateTime dtMSH;
            if (DateTime.TryParseExact(segMSH.Field(MSH_MSG_TIME), "yyyyMMddHHmmsszzz", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out dtMSH))
            {
                return dtMSH.ToString("yyyy/MM/dd HH:mm:ss");
            }
            if (DateTime.TryParseExact(segMSH.Field(MSH_MSG_TIME), "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out dtMSH))
            {
                return dtMSH.ToString("yyyy/MM/dd HH:mm:ss");
            }
            if (DateTime.TryParseExact(segMSH.Field(MSH_MSG_TIME), "yyyyMMddHHmm", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out dtMSH))
            {
                return dtMSH.ToString("yyyy/MM/dd HH:mm:ss");
            }

            return null;
        }

        public void Add(Segment _Segment)
        {
            if (!string.IsNullOrEmpty(_Segment.Name) && _Segment.Name.Length == 3)
            {
                _Segments.AddLast(_Segment);
            }
        }

        public Segment FindSegment(string _Name)
        {
            foreach (Segment objSegment in _Segments)
            {
                if (objSegment.Name == _Name)
                {
                    return objSegment;
                }
            }
            return null;
        }

        public Segment FindPreviousSegment(string _Name, Segment _Segment)
        {
            var Node = _Segments.Find(_Segment);
            if (Node == null)
            {
                throw new NullReferenceException();
            }

            while (Node.Previous != null)
            {
                Node = Node.Previous;
                if (Node.Value.Name == _Name)
                {
                    return Node.Value;
                }
            }

            return null;
        }

        public Segment FindNextSegment(string _Name, Segment _Segment)
        {
            var Node = _Segments.Find(_Segment);
            if (Node == null)
            {
                throw new NullReferenceException();
            }

            while (Node.Next != null)
            {
                Node = Node.Next;
                if (Node.Value.Name == _Name)
                {
                    return Node.Value;
                }
            }
            return null;
        }

        public void Parse(string _Text)
        {
            Clear();
            char[] chrDelimiter = { '\r' };
            string[] aryTokens = _Text.Split(chrDelimiter, StringSplitOptions.None);

            foreach (string strItem in aryTokens)
            {
                Segment objSegment = new Segment();
                objSegment.Parse(strItem.Trim('\n'));
                Add(objSegment);
            }
        }

        public string Serialize()
        {
            StringBuilder sbTemp = new StringBuilder();
            char[] chrDelimiter = { '\r', '\n' };

            foreach (Segment objSegment in _Segments)
            {
                sbTemp.Append(objSegment.Serialize());
                sbTemp.Append("\r\n");
            }
            return sbTemp.ToString();
        }

        public void JsonParse(string _JsonText, string[] _aryHeader, XmlNode _XmlNode)
        {
            Clear();
            JObject Obj = null;
            DateTime dtNow = DateTime.Now;

            if (!string.IsNullOrEmpty(_JsonText))
            {
                Obj = JsonConvert.DeserializeObject<JObject>(_JsonText);
            }

            foreach (string Name in _aryHeader)
            {
                Segment objSegment = new Segment();
                objSegment.JsonParse(Obj, Name, dtNow, _XmlNode);
                Add(objSegment);
            }
        }

        public string JsonSerialize(string[] _aryJson, XmlNode _XmlNode)
        {
            JObject objJObject = new JObject();

            foreach (string Name in _aryJson)
            {
                string XmlFormat = "";
                string objValue = FindSegmentValue(Name, _XmlNode, XmlFormat);

                if (XmlFormat.Equals("Integer"))
                {
                    objJObject.Add(new JProperty(Name, int.Parse(objValue)));
                }
                else
                {
                    objJObject.Add(new JProperty(Name, objValue));
                }
            }

            Console.WriteLine("JSON:{0}", JsonConvert.SerializeObject(objJObject));

            return JsonConvert.SerializeObject(objJObject, Newtonsoft.Json.Formatting.Indented);

        }

        public string SetSegmentValue(string _DataTag, string _DataValue, XmlNode _XmlNode)
        {
            XmlNodeList xmlNodeList = _XmlNode.SelectNodes("Element[@DataTag='" + _DataTag + "']");
            foreach (XmlElement xmlElement in xmlNodeList)
            {
                string objCode = xmlElement.GetAttribute("Code");
                string objPosition = xmlElement.GetAttribute("Position");
                string objPositionItem = xmlElement.GetAttribute("PositionItem");

                Segment objSegment = FindSegment(objCode);

                if (objSegment != null)
                {
                    try
                    {
                        int position = 0;
                        int.TryParse(objPosition, out position);
                        int positionItem = 0;
                        int.TryParse(objPosition, out positionItem);

                        if (string.IsNullOrEmpty(objPositionItem))
                        {
                            objSegment.Field(position, _DataValue);
                            return objSegment.Field(position);
                        }
                        else
                        {
                            var aryValue = objSegment.Field(position).Split('^');
                            if (aryValue.Count() >= positionItem)
                            {
                                aryValue[positionItem] = _DataValue;
                                objSegment.Field(position, string.Join("^", aryValue));
                                return aryValue[positionItem];
                            }
                        }
                    }
                    catch (Exception ex)
                    {

                    }
                }
            }
            return "";
        }

        public string FindSegmentValue(string _DataTag, XmlNode _XmlNode, string _XmlFormat = "")
        {
            XmlNodeList xmlNodeList = _XmlNode.SelectNodes("Element[@DataTag='" + _DataTag + "']");
            if (xmlNodeList.Count > 0)
            {
                //XML 有DataTag的
                foreach (XmlElement xmlElement in xmlNodeList)
                {
                    string objCode = xmlElement.GetAttribute("Code");
                    string objPosition = xmlElement.GetAttribute("Position");
                    string objPositionItem = xmlElement.GetAttribute("PositionItem");
                    string objMaxLen = xmlElement.GetAttribute("MaxLen");

                    if (objMaxLen.Equals("14.4"))
                    {
                        _XmlFormat = "Integer";
                    }

                    Segment objSegment = FindSegment(objCode);
                    if (objSegment != null)
                    {
                        int position = 0;
                        int.TryParse(objPosition, out position);
                        int positionItem = 0;
                        int.TryParse(objPositionItem, out positionItem);

                        try
                        {
                            if (string.IsNullOrEmpty(objPositionItem))
                            {
                                return objSegment.Field(position);
                            }
                            else
                            {
                                var aryValue = objSegment.Field(position).Split('^');
                                if (aryValue.Count() >= positionItem)
                                {
                                    return aryValue[positionItem];
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            return "";
                        }
                    }
                }
            }
            else
            {
                //讀取 Extend XML 資料比對
                XmlNodeList xmlNodeExtendList = _XmlNode.SelectNodes("ExtendElement[@ExtTag='" + _DataTag + "' ]");
                foreach (XmlElement xmlElement in xmlNodeExtendList)
                {
                    string objMaxLen = xmlElement.GetAttribute("MaxLen");
                    string objValue = FindSegmentValue(xmlElement.GetAttribute("DataTag"), _XmlNode);

                    foreach (XmlElement xmlData in xmlElement.ChildNodes)
                    {
                        string objDataValue = xmlData.GetAttribute("DataValue");
                        string objExtValue = xmlData.GetAttribute("ExtValue");

                        if (objValue == objDataValue)
                        {
                            return objExtValue;
                        }
                    }
                }
                return "";
            }
            return "";
        }
    }
}
