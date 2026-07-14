using System;
using System.Collections.Generic;
using System.Text;

namespace Qomicex.Core.AOT.Models
{
    /// <summary>
    /// Represents user authentication information.
    /// </summary>
    /// <param name="Name"></param>
    /// <param name="Uuid"></param>
    /// <param name="Token"></param>
    /// <param name="AccessToken"></param>
    /// <param name="RefreshToken"></param>
    public record UserAuth(string Name,string Uuid,string Token,string AccessToken,string RefreshToken);
}
