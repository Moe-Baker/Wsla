using UnityEditor;

using UnityEngine;

namespace Toolbox
{
    public static class PlatformUtility
    {
        /// <summary>
        /// Gets Runtime platform based on build target in editor, and based on runtime platform in players
        /// </summary>
        /// <returns></returns>
        public static RuntimePlatform GetBuildPlatform()
        {
#if UNITY_EDITOR
#pragma warning disable CS0618 // Type or member is obsolete
            switch (EditorUserBuildSettings.activeBuildTarget)
            {
                case BuildTarget.StandaloneOSX:
                    return RuntimePlatform.OSXPlayer;

                case BuildTarget.iOS:
                    return RuntimePlatform.IPhonePlayer;

                case BuildTarget.Android:
                    return RuntimePlatform.Android;

                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return RuntimePlatform.WindowsPlayer;

                case BuildTarget.EmbeddedLinux:
                case BuildTarget.LinuxHeadlessSimulation:
                case BuildTarget.StandaloneLinux64:
                    return RuntimePlatform.LinuxPlayer;

                case BuildTarget.WebGL:
                    return RuntimePlatform.WebGLPlayer;

                case BuildTarget.WSAPlayer:
                    return RuntimePlatform.WSAPlayerX64;

                case BuildTarget.PS4:
                    return RuntimePlatform.PS4;

                case BuildTarget.XboxOne:
                    return RuntimePlatform.XboxOne;

                case BuildTarget.tvOS:
                    return RuntimePlatform.tvOS;

                case BuildTarget.Switch:
                    return RuntimePlatform.Switch;

                case BuildTarget.Stadia:
                    return RuntimePlatform.Stadia;
            }
#pragma warning restore CS0618 // Type or member is obsolete
#endif

            return Application.platform;
        }
    }
}