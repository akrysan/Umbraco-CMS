using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Umbraco.Core.Configuration;
using Umbraco.Core.IO;

namespace Umbraco.Core
{
	/// <summary>
	/// The XmlHelper class contains general helper methods for working with xml in umbraco.
    /// </summary>
    public class XmlHelper
    {
	    /// <summary>
	    /// Sorts the children of the parentNode that match the xpath selector 
	    /// </summary>
	    /// <param name="parentNode"></param>
	    /// <param name="childXPathSelector">An xpath expression used to select child nodes of the XmlElement</param>
	    /// <param name="childSelector">An expression that returns true if the XElement passed in is a valid child node to be sorted</param>
	    /// <param name="orderByValue">The value to order the results by</param>
	    internal static void SortNodes(
            XmlNode parentNode, 
            string childXPathSelector, 
            Func<XElement, bool> childSelector,
            Func<XElement, object> orderByValue)
	    {

            var xElement = parentNode.ToXElement();
            var children = xElement.Elements().Where(x => childSelector(x)).ToArray(); //(DONT conver to method group, the build server doesn't like it)
            
            var data = children
                .OrderByDescending(orderByValue)     //order by the sort order desc
                .Select(x => children.IndexOf(x))   //store the current item's index (DONT conver to method group, the build server doesn't like it)
                .ToList();

            //get the minimum index that a content node exists  in the parent
            var minElementIndex = xElement.Elements()
                .TakeWhile(x => childSelector(x) == false)
                .Count();

	        //if the minimum index is zero, then it is the very first node inside the parent,
            // if it is not, we need to store the child property node that exists just before the 
            // first content node found so we can insert elements after it when we're sorting.
            var insertAfter = minElementIndex == 0 ? null : parentNode.ChildNodes[minElementIndex - 1];

            var selectedChildren = parentNode.SelectNodes(childXPathSelector);
            if (selectedChildren == null)
            {
                throw new InvalidOperationException(string.Format("The childXPathSelector value did not return any results {0}", childXPathSelector));
            }

            var childElements = selectedChildren.Cast<XmlElement>().ToArray();

            //iterate over the ndoes starting with the node with the highest sort order.
            //then we insert this node at the begginning of the parent so that by the end
            //of the iteration the node with the least sort order will be at the top.
            foreach (var node in data.Select(index => childElements[index]))
            {
                if (insertAfter == null)
                {
                    parentNode.PrependChild(node);
                }
                else
                {
                    parentNode.InsertAfter(node, insertAfter);
                }
            }
        }

        public static string StripDashesInElementOrAttributeNames(string xml)
        {
            using (var outputms = new MemoryStream())
            {
                using (TextWriter outputtw = new StreamWriter(outputms))
                {
                    using (var ms = new MemoryStream())
                    {
                        using (var tw = new StreamWriter(ms))
                        {
                            tw.Write(xml);
                            tw.Flush();
                            ms.Position = 0;
                            using (var tr = new StreamReader(ms))
                            {
                                bool IsInsideElement = false, IsInsideQuotes = false;
                                int ic = 0;
                                while ((ic = tr.Read()) != -1)
                                {
                                    if (ic == (int)'<' && !IsInsideQuotes)
                                    {
                                        if (tr.Peek() != (int)'!')
                                        {
                                            IsInsideElement = true;
                                        }
                                    }
                                    if (ic == (int)'>' && !IsInsideQuotes)
                                    {
                                        IsInsideElement = false;
                                    }
                                    if (ic == (int)'"')
                                    {
                                        IsInsideQuotes = !IsInsideQuotes;
                                    }
                                    if (!IsInsideElement || ic != (int)'-' || IsInsideQuotes)
                                    {
                                        outputtw.Write((char)ic);
                                    }
                                }

                            }
                        }
                    }
                    outputtw.Flush();
                    outputms.Position = 0;
                    using (TextReader outputtr = new StreamReader(outputms))
                    {
                        return outputtr.ReadToEnd();
                    }
                }
            }
        }

		/// <summary>
        /// Imports a XML node from text.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="xmlDoc">The XML doc.</param>
        /// <returns></returns>
		public static XmlNode ImportXmlNodeFromText(string text, ref XmlDocument xmlDoc)
        {
            xmlDoc.LoadXml(text);
            return xmlDoc.FirstChild;
        }

        /// <summary>
        /// Opens a file as a XmlDocument.
        /// </summary>
        /// <param name="filePath">The relative file path. ei. /config/umbraco.config</param>
        /// <returns>Returns a XmlDocument class</returns>
        public static XmlDocument OpenAsXmlDocument(string filePath)
        {

        	var reader = new XmlTextReader(IOHelper.MapPath(filePath)) {WhitespaceHandling = WhitespaceHandling.All};

        	var xmlDoc = new XmlDocument();
            //Load the file into the XmlDocument
            xmlDoc.Load(reader);
            //Close off the connection to the file.
            reader.Close();

            return xmlDoc;
        }

        /// <summary>
        /// creates a XmlAttribute with the specified name and value
        /// </summary>
        /// <param name="xd">The xmldocument.</param>
        /// <param name="name">The name of the attribute.</param>
        /// <param name="value">The value of the attribute.</param>
        /// <returns>a XmlAttribute</returns>
        public static XmlAttribute AddAttribute(XmlDocument xd, string name, string value)
        {
            var temp = xd.CreateAttribute(name);
            temp.Value = value;
            return temp;
        }

        /// <summary>
        /// Creates a text XmlNode with the specified name and value
        /// </summary>
        /// <param name="xd">The xmldocument.</param>
        /// <param name="name">The node name.</param>
        /// <param name="value">The node value.</param>
        /// <returns>a XmlNode</returns>
        public static XmlNode AddTextNode(XmlDocument xd, string name, string value)
        {
            var temp = xd.CreateNode(XmlNodeType.Element, name, "");
            temp.AppendChild(xd.CreateTextNode(value));
            return temp;
        }

        /// <summary>
        /// Creates a cdata XmlNode with the specified name and value
        /// </summary>
        /// <param name="xd">The xmldocument.</param>
        /// <param name="name">The node name.</param>
        /// <param name="value">The node value.</param>
        /// <returns>A XmlNode</returns>
		public static XmlNode AddCDataNode(XmlDocument xd, string name, string value)
        {
            var temp = xd.CreateNode(XmlNodeType.Element, name, "");
            temp.AppendChild(xd.CreateCDataSection(value));
            return temp;
        }

        /// <summary>
        /// Gets the value of a XmlNode
        /// </summary>
        /// <param name="n">The XmlNode.</param>
        /// <returns>the value as a string</returns>
		public static string GetNodeValue(XmlNode n)
        {
            var value = string.Empty;
            if (n == null || n.FirstChild == null)
                return value;
            value = n.FirstChild.Value ?? n.InnerXml;
            return value.Replace("<!--CDATAOPENTAG-->", "<![CDATA[").Replace("<!--CDATACLOSETAG-->", "]]>");
        }

        /// <summary>
        /// Determines whether the specified string appears to be XML.
        /// </summary>
        /// <param name="xml">The XML string.</param>
        /// <returns>
        /// 	<c>true</c> if the specified string appears to be XML; otherwise, <c>false</c>.
        /// </returns>
		public static bool CouldItBeXml(string xml)
        {
            if (!string.IsNullOrEmpty(xml))
            {
                xml = xml.Trim();

                if (xml.StartsWith("<") && xml.EndsWith(">"))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Splits the specified delimited string into an XML document.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="separator">The separator.</param>
        /// <param name="rootName">Name of the root.</param>
        /// <param name="elementName">Name of the element.</param>
        /// <returns>Returns an <c>System.Xml.XmlDocument</c> representation of the delimited string data.</returns>
		public static XmlDocument Split(string data, string[] separator, string rootName, string elementName)
        {
            return Split(new XmlDocument(), data, separator, rootName, elementName);
        }

        /// <summary>
        /// Splits the specified delimited string into an XML document.
        /// </summary>
        /// <param name="xml">The XML document.</param>
        /// <param name="data">The delimited string data.</param>
        /// <param name="separator">The separator.</param>
        /// <param name="rootName">Name of the root node.</param>
        /// <param name="elementName">Name of the element node.</param>
        /// <returns>Returns an <c>System.Xml.XmlDocument</c> representation of the delimited string data.</returns>
		public static XmlDocument Split(XmlDocument xml, string data, string[] separator, string rootName, string elementName)
        {
            // load new XML document.
            xml.LoadXml(string.Concat("<", rootName, "/>"));

            // get the data-value, check it isn't empty.
            if (!string.IsNullOrEmpty(data))
            {
                // explode the values into an array
                var values = data.Split(separator, StringSplitOptions.None);

                // loop through the array items.
                foreach (string value in values)
                {
                    // add each value to the XML document.
                    var xn = XmlHelper.AddTextNode(xml, elementName, value);
                    xml.DocumentElement.AppendChild(xn);
                }
            }

            // return the XML node.
            return xml;
        }

		/// <summary>
		/// Return a dictionary of attributes found for a string based tag
		/// </summary>
		/// <param name="tag"></param>
		/// <returns></returns>
		public static Dictionary<string, string> GetAttributesFromElement(string tag)
		{
			var m =
				Regex.Matches(tag, "(?<attributeName>\\S*)=\"(?<attributeValue>[^\"]*)\"",
							  RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
			// fix for issue 14862: return lowercase attributes for case insensitive matching
			var d = m.Cast<Match>().ToDictionary(attributeSet => attributeSet.Groups["attributeName"].Value.ToString().ToLower(), attributeSet => attributeSet.Groups["attributeValue"].Value.ToString());
			return d;
		}
    }
}
