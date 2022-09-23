using System;
using System.Xml.Serialization;

namespace X410Launcher.Models.Appx;

[Serializable, XmlRoot(Namespace = Namespaces.AppxManifestFoundationWindows10, ElementName = nameof(Package))]
public class Package
{
    [XmlElement(nameof(Identity))]
    public Identity Identity { get; set; } = new();
}
