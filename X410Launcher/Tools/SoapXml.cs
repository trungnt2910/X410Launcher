using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace X410Launcher.Tools;

public class SoapXml
{
    public class Node
    {
        private readonly XmlNode? _node;
        private readonly XmlNamespaceManager _nsmgr;
        public string InnerText => _node?.InnerText ?? string.Empty;

        public Node(XmlNode? node, XmlNamespaceManager? nsmgr)
        {
            _node = node;
            _nsmgr = nsmgr ?? new(new NameTable());
        }

        public IEnumerable<Node> SelectNodes(string xpath)
        {
            return (_node?.SelectNodes(xpath, _nsmgr)?.Cast<XmlNode>() ?? [])
                .Select(node => new Node(node, _nsmgr));
        }

        public Node SelectSingleNode(string xpath)
        {
            return new Node(_node?.SelectSingleNode(xpath, _nsmgr), _nsmgr);
        }

        public string GetAttribute(string name)
        {
            return _node?.Attributes?[name]?.Value ?? string.Empty;
        }
    }

    private readonly XmlDocument _xml = new();
    private readonly Node _header;
    private readonly Node _security;
    private readonly Node _body;

    public Node Body => _body;

    public string Expires => _security.SelectSingleNode("./u:Timestamp/u:Expires").InnerText;

    public SoapXml(string contents)
    {
        _xml.LoadXml(contents);

        var header = _xml.GetElementsByTagName("s:Header")[0]?.FirstChild as XmlElement;
        var security = _xml.GetElementsByTagName("o:Security")[0] as XmlElement;
        var body = _xml.GetElementsByTagName("s:Body")[0]?.FirstChild as XmlElement;

        if (header == null || security == null || body == null)
        {
            throw new ArgumentException("Invalid SOAP XML.");
        }

        _header = new Node(header, GetPopulatedNamespaceManager(header));
        _security = new Node(security, GetPopulatedNamespaceManager(security));
        _body = new Node(body, GetPopulatedNamespaceManager(body));
    }

    private XmlNamespaceManager GetPopulatedNamespaceManager(XmlElement element)
    {
        var nsmgr = new XmlNamespaceManager(new NameTable());

        var namespaces = element.CreateNavigator()?.GetNamespacesInScope(XmlNamespaceScope.All);
        if (namespaces != null)
        {
            foreach (var kvp in namespaces)
            {
                nsmgr.AddNamespace(kvp.Key, kvp.Value);
            }
        }
        nsmgr.AddNamespace("this", element.NamespaceURI);

        return nsmgr;
    }
}
