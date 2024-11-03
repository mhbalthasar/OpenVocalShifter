using DotnetWorld.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenVoiceShifter
{
    public class WordInitialize
    {
        /// <summary>
        /// 这个初始化的作用用于保证DLL被正确加载
        /// </summary>
        #region
        private static bool InitedDll = false;
        public static void Initialize()
        {
            try
            {
                if (InitedDll) return;
                Core.InitDllFile();
                InitedDll = true;
            }
            catch {; }
        }
        #endregion
    }
}
