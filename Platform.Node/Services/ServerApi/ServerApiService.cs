using System;
using System.Threading;
using System.Threading.Tasks;
using ServiceStack.Plugins.Tasks;
using ServiceStack.ServiceInterface;
using ServiceStack.WebHost.Endpoints;

namespace Platform.Node
{
    public class ServiceStackHost : AppHostHttpListenerBase
    {
        readonly IPublisher _publisher;

        public ServiceStackHost(IPublisher publisher) : base("Lokad DataPlatform API (raw)", typeof(StreamService).Assembly)
        {
            _publisher = publisher;
        }

        public override void Configure(Funq.Container container)
        {
            LoadPlugin(new TaskSupport());
            Routes
                //.Add<ClientDto.CreateStream>("/stream", "PUT")
                //.Add<ClientDto.CreateStream>("/stream/create/{Name}")
                .Add<ClientDto.WriteEvent>("/stream/", "POST")
                .Add<ClientDto.WriteEvent>("/stream/{Stream}", "POST");

            container.Register(_publisher);
        }
    }

    public static class ClientDto
    {
        public class WriteEvent
        {
            public string Stream { get; set; }
            public byte[] Data { get; set; }
            public int ExpectedVersion { get; set; }
        }

        public class WriteEventResponse
        {
            public string Result { get; set; }
        }

    }


    public class PlatformServerApiService : IHandle<SystemMessage.Init>, IHandle<SystemMessage.Shutdown>
    {
        readonly IPublisher _publisher;
        readonly string _url;
        ServiceStackHost _host;
        public PlatformServerApiService(IPublisher publisher, string url)
        {
            _publisher = publisher;
            _url = url;
        }

        public void Handle(SystemMessage.Init message)
        {
            _host = new ServiceStackHost(_publisher);
            _host.Init();
            try
            {
                _host.Start(_url);
            }
            catch(Exception ex)
            {
                Application.Exit(ExitCode.Error, "Failed to start Http server: " + ex.Message);
            }
        }

        public void Handle(SystemMessage.Shutdown message)
        {
            _host.Stop();
        }
    }

    public class StreamService : ServiceBase<ClientDto.WriteEvent>
    {
        readonly IPublisher _publisher;
        
        public StreamService(IPublisher publisher)
        {
            _publisher = publisher;
        }


        protected override object Run(ClientDto.WriteEvent request)
        {
            var token = new ManualResetEventSlim(false);

            ClientMessage.AppendEventsCompleted hack;
            _publisher.Publish(new ClientMessage.AppendEvents(request.Stream,request.Data, request.ExpectedVersion,s =>
            {
                hack = s;
                token.Set();
            }));



            return Task.Factory.StartNew(() =>
            {
                token.Wait();
                return new ClientDto.WriteEventResponse()
                {
                    Result = "Completed"
                };
            });
        }
    }

}