using System;
using System.Xml.Serialization;

namespace X410Launcher.Models.Appx;

[Serializable]
public enum ProcessorArchitecture
{
    [XmlEnum("x86")]
    X86,
    [XmlEnum("x64")]
    X64,
    [XmlEnum("arm")]
    Arm,
    [XmlEnum("arm64")]
    Arm64
}
