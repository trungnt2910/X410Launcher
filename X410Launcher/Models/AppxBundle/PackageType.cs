using System;
using System.Xml.Serialization;

namespace X410Launcher.Models.AppxBundle;

[Serializable]
public enum PackageType
{
    [XmlEnum(Name = "application")]
    Application,
    [XmlEnum(Name = "resource")]
    Resource
}
