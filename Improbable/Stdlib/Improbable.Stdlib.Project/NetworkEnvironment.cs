
namespace Improbable.Stdlib.Project
{
    public readonly struct NetworkEnvironment
    {
        public readonly string RuntimeIp;

        public readonly string ServiceIp;

        public readonly ushort ServicePort;

        public readonly bool Secure;

        public NetworkEnvironment(string runtimeIp, string serviceIp, ushort servicePort, bool secure)
        {
            RuntimeIp = runtimeIp;
            ServiceIp = serviceIp;
            ServicePort = servicePort;
            Secure = secure;
        }

        public static readonly NetworkEnvironment Local = new NetworkEnvironment("127.0.0.1", "127.0.0.1", 9876, false);
    }
}
