using System;
using System.Collections.Generic;
using System.Text;

namespace IISManager
{
    internal class AppPoolNotFoundException : Exception
    {
        public AppPoolNotFoundException() : base("Application Pool with the specified name does not exist.")
        { }

        public AppPoolNotFoundException(string val) : base($"Application Pool '{val}' does not exist.")
        { }
    }
}
