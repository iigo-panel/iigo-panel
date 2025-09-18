﻿// Copyright (c) Lex Li. All rights reserved.
// 
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;

namespace Microsoft.Web.Administration
{
    public sealed class BindingCollection : ConfigurationElementCollectionBase<Binding>
    {
        internal BindingCollection(Site parent)
            : this(null, parent)
        { }

        internal BindingCollection(ConfigurationElement element, Site parent)
            : base(element, "bindings", parent.Schema.ChildElementSchemas["bindings"], parent, element?.InnerEntity, null)
        {
            Parent = parent;
            if (element != null)
            {
                foreach (ConfigurationElement child in (ICollection)element)
                {
                    InternalAdd(new Binding(child, this));
                }
            }
        }

        public Binding Add(string bindingInformation, string bindingProtocol)
        {
            var item = new Binding(bindingProtocol, bindingInformation, null, null, SslFlags.None, this);
            InternalAdd(item);
            return item;
        }

        public Binding Add(string bindingInformation, byte[] certificateHash, string certificateStoreName)
        {
            return Add(bindingInformation, certificateHash, certificateStoreName, SslFlags.None);
        }

        public Binding Add(string bindingInformation, byte[] certificateHash, string certificateStoreName, SslFlags sslFlags)
        {
            var item = new Binding("https", bindingInformation, certificateHash, certificateStoreName, sslFlags, this);
            InternalAdd(item);
            return item;
        }

        protected override Binding CreateNewElement(string elementTagName)
        {
            return new Binding(null, this);
        }

        public void Remove(Binding element, bool removeConfigOnly)
        {
            element.Delete();
            if (!removeConfigOnly)
            {
                Remove(element);
            }
        }

        internal Site Parent { get; set; }

        internal bool ElevationRequired
        {
            get
            {
                foreach (Binding binding in this)
                {
                    if (binding.EndPoint?.Port <= 1024)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
