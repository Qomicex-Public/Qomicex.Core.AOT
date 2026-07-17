using System;
using System.Collections.Generic;
using System.Text;

namespace Qomicex.Core.AOT.Public.Models
{
    public record MissFileInfo(
        string Name,
        string Url,
        string Sha1,
        string Path
        );
}
