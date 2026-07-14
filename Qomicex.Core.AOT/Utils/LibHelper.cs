using Qomicex.Core.AOT.Models.VersionMetadata;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Text.Json.Nodes;

namespace Qomicex.Core.AOT.Utils
{
    public sealed class LibHelper
    {
        public static bool IsClassPath(Library library)
        {
            if (library.Downloads is not null)
            {
                if (library.Downloads.Artifact is not null)
                    return true;
            }
            else
            {
                if (library.Natives is null)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsNatives(Library library)
        {
            if (library.Natives is not null)
                return true;
            if (library.Downloads is not null && library.Downloads.Classifiers is not null)
                return true;
            if (library.Name.ToLower().Contains("natives"))
                return true;
            return false;
        }

        public static bool IsRuleSuitable(Models.VersionMetadata.Rule rule)
        {
            if (rule.Action == "allow")
            {
                if (rule.Os is not null && rule.Os.Name is not null)
                {
                    if (SystemHelper.IsOsMatch(rule.Os))
                    {
                        return true;
                    }
                }
                else
                {
                    return true;
                }
            }
            else if (rule.Action == "disallow")
            {
                if (rule.Os is not null && rule.Os.Name is not null)
                {
                    if (SystemHelper.IsOsMatch(rule.Os))
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            return false;
        }
    }
}
