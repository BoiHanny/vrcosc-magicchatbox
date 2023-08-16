using System;
using System.Timers;
using System.Collections.Generic;
using System.Threading.Tasks;
using VRC.OSCQuery;

namespace vrcosc_magicchatbox.Classes
{
    public class OscQueryController
    {
        private class OSCSender
        {
            public OSCQueryService OscQuery { get; private set; }

            public OSCSender(string serviceName)
            {
                var tcpPort = Extensions.GetAvailableTcpPort();
                var udpPort = Extensions.GetAvailableUdpPort();

                OscQuery = new OSCQueryServiceBuilder()
                    .WithDefaults()
                    .WithTcpPort(tcpPort)
                    .WithUdpPort(udpPort)
                    .WithServiceName(serviceName)
                    .Build();

                Console.WriteLine($"Started {serviceName} at TCP {tcpPort}, UDP {udpPort}");
            }

            public void Shutdown()
            {
                OscQuery.Dispose();
            }

            public void AddEndpoint(string path, string type, string description)
            {
                OscQuery.AddEndpoint(path, type, Attributes.AccessValues.WriteOnly, new object[] { description });

            }
        }

        private List<OSCSender> _senders;
        private List<OSCQueryServiceProfile> _profiles;

        public OscQueryController()
        {
            _senders = new List<OSCSender>
        {
            new OSCSender("MagicChatbox"),
            //new OSCSender("MagicChatbox2nd")
        };

            _profiles = new List<OSCQueryServiceProfile>();
        }

        public void Shutdown()
        {
            foreach (var sender in _senders)
            {
                sender.Shutdown();
            }
        }

        public void AddEndpointToAllSenders(string path, string type, string description)
        {
            foreach (var sender in _senders)
            {
                sender.AddEndpoint(path, type, description);
            }
        }

        public void DiscoverServices()
        {
            foreach (var sender in _senders)
            {
                foreach (var service in sender.OscQuery.GetOSCQueryServices())
                {
                    AddProfileToList(service);
                }

                sender.OscQuery.OnOscQueryServiceAdded += AddProfileToList;

                // Refresh every 5 seconds
                var refreshTimer = new Timer(5000);
                refreshTimer.Elapsed += (s, e) => sender.OscQuery.RefreshServices();
                refreshTimer.Start();
            }
        }

        private void AddProfileToList(OSCQueryServiceProfile profile)
        {
            _profiles.Add(profile);
            Console.WriteLine($"Added {profile.name} to list of profiles");
        }

        public async Task<bool> CheckServiceForEndpoint(OSCQueryServiceProfile profile, string endpointPath)
        {
            var tree = await Extensions.GetOSCTree(profile.address, profile.port);
            var node = tree.GetNodeWithPath(endpointPath);
            return node != null;
        }

        public async Task<List<OSCQueryServiceProfile>> FindServicesWithEndpoint(string endpointPath)
        {
            List<OSCQueryServiceProfile> servicesWithEndpoint = new List<OSCQueryServiceProfile>();
            foreach (var profile in _profiles)
            {
                if (await CheckServiceForEndpoint(profile, endpointPath))
                {
                    servicesWithEndpoint.Add(profile);
                }
            }
            return servicesWithEndpoint;
        }
    }
}
