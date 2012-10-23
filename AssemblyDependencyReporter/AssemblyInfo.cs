using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AssemblyDependencyReporter
{
    internal struct AssemblyInfo 
    {
        internal readonly String Name;
        internal readonly Int32 Depth;
        internal readonly AssemblyRefStatus Status;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="depth"></param>
        public AssemblyInfo(String name, Int32 depth = 0, AssemblyRefStatus status = AssemblyRefStatus.None)
        {
            this.Name = name;
            this.Depth = depth;
            this.Status = status;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            if (obj is AssemblyInfo)
            {
                AssemblyInfo other = (AssemblyInfo)obj;
                return other.Name == this.Name;
            }

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override Int32 GetHashCode()
        {
            return this.Name.GetHashCode();
        }
    }

    internal enum AssemblyRefStatus
    {
        /// <summary>
        /// Ok
        /// </summary>
        None = 0,
        
        /// <summary>
        /// Null Reference
        /// </summary>
        Null = 1,
        
        /// <summary>
        /// Assembly Not Found
        /// </summary>
        NotFound = 2,

        /// <summary>
        /// Assembly corrupt
        /// </summary>
        BadImage = 3,

        /// <summary>
        /// Security exception (no permission)
        /// </summary>
        NoPermission = 4,
    }
}
