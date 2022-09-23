using System;
using System.Security.RightsManagement;
using System.Xml.Serialization;

namespace X410Launcher.Models.AppxBundle;

[Serializable, XmlType(Namespace = Namespaces.Appx2013Bundle, TypeName = nameof(Package))]
public class Package
{
    [XmlAttribute(nameof(Type))]
    public PackageType Type { get; set; }

    [XmlAttribute(nameof(Version))]
    public string VersionString { get; set; } = new Version().ToString();

    [XmlIgnore]
    public Version Version => Version.Parse(VersionString);

    [XmlAttribute(nameof(Architecture))]
    public PackageArchitecture Architecture { get; set; }

    [XmlAttribute(nameof(FileName))]
    public string FileName { get; set; } = string.Empty;

    [XmlAttribute(nameof(Offset))]
    public long Offset { get; set; }

    [XmlAttribute(nameof(Size))]
    public long Size { get; set; }
}
