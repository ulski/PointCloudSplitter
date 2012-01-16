using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PointCloudSplitter
{
    ///
    /// <project>Calib3D http://code.google.com/p/cam-calib3d/ </project>
    /// <author>Christoph Heindl</author>
    /// <copyright>Copyright (c) 2011, Christoph Heindl</copyright>
    /// <license>New BSD License</license>
    ///
    /// 
    /// <summary>
    /// Provides a unique interface to default values for refernce and value types.
    /// </summary>
    /// <remarks>
    /// To my knowledge, .NET does not provide any unified access to 
    /// defaults for reference types. Hence this class. It unifies
    /// the interface to access custom default values for value types and reference
    /// types.
    /// 
    /// For value types, the method GetValueOrDefault of System.Nullable can be used.
    /// For reference types, this class emulates GetValueOrDefault by comparing the
    /// the reference type against null. In case it is null, the user provided default
    /// value is returned, otherwise the object itself.
    /// </remarks>
    public static class Default
    {

        public static T Get<T>(Nullable<T> v) where T : struct
        {
            return v.GetValueOrDefault();
        }

        public static T Get<T>(Nullable<T> v, T default_value) where T : struct
        {
            return v.GetValueOrDefault(default_value);
        }

        public static T Get<T>(T v, T default_value) where T : class
        {
            if (v == null)
            {
                return default_value;
            }
            else
            {
                return v;
            }
        }

    }
}
