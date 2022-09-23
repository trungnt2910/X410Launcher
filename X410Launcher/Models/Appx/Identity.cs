using System;
using System.Xml.Serialization;

namespace X410Launcher.Models.Appx;

[Serializable, XmlType(Namespace = Namespaces.AppxManifestFoundationWindows10, TypeName = nameof(Identity))]
public class Identity
{
    [XmlAttribute(nameof(Name))]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute(nameof(Publisher))]
    public string Publisher { get; set; } = string.Empty;

    [XmlAttribute(nameof(Version))]
    public string VersionString { get; set; } = new Version().ToString();

    [XmlIgnore]
    public Version Version => Version.Parse(VersionString);

    [XmlAttribute(nameof(ProcessorArchitecture))]
    public ProcessorArchitecture ProcessorArchitecture { get; set; }
}
