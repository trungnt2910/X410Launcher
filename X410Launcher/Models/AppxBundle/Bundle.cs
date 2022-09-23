using System;
using System.Xml.Serialization;

namespace X410Launcher.Models.AppxBundle;

[Serializable, XmlRoot(Namespace = Namespaces.Appx2013Bundle, ElementName = nameof(Bundle))]
public class Bundle
{
    [XmlElement(nameof(Identity))]
    public Identity Identity { get; set; } = new();

    [XmlArray(nameof(Packages))]
    [XmlArrayItem(nameof(Package))]
    public Package[] Packages { get; set; } = Array.Empty<Package>();
}
