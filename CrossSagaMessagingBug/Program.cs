using System;
using System.Diagnostics;
using NServiceBus;
using NServiceBus.Features;
using NServiceBus.Saga;

namespace CrossSagaMessagingBug
{
    class Program
    {
        static IBus Bus { get; set; }

        static void Main()
        {
            SetLoggingLibrary.Log4Net();
            Configure.Features.Enable<Sagas>();
            Bus = Configure.With()
                .Log4Net()
                           .DefaultBuilder()
                           .InMemorySagaPersister()
                           .UseInMemoryTimeoutPersister()
                           .InMemoryFaultManagement()
                           .UnicastBus()
                           .CreateBus()
                           .Start();

            Bus.SendLocal(new StartFirstSaga());

            Console.ReadLine();
        }
    }

    public class Test : IHandleSagaNotFound
    {
        public void Handle(object message)
        {
            if (Debugger.IsAttached) Debugger.Break();
            throw new Exception("This message should have started the second saga, but didn't because it has SagaType and SagaId headers, can't find the SagaData for FirstSaga, and won't create the saga data for SecondSaga...");
        }
    }

    public class FirstSaga : Saga<FirstSagaData>, IAmStartedByMessages<StartFirstSaga>, IHandleTimeouts<FirstSagaTimeout>
    {
        public void Handle(StartFirstSaga message)
        {
            // If I try to start the second saga here, everything is OK
            //Bus.SendLocal(new StartSecondSaga());

            // But if I request a timeout and then attempt to start the second Saga I go through the "SagaNotFound handlers"
            RequestTimeout(TimeSpan.FromSeconds(1), new FirstSagaTimeout());
        }

        public void Timeout(FirstSagaTimeout state)
        {
            // Sending this message copies the SagaType and SagaId headers which causes the saga dispatcher to fail finding the Saga data
            // and for some reason the saga data isn't created even though SecondSaga is started by the StartSecondSaga message
            Bus.SendLocal(new StartSecondSaga());
            MarkAsComplete();
        }
    }

    public class SecondSaga : Saga<SecondSagaData>, IAmStartedByMessages<StartSecondSaga>
    {
        public void Handle(StartSecondSaga message)
        {
            // This is never called because the saga dispatcher can't find Saga Data and won't create it.
            MarkAsComplete();
        }
    }

    public class StartFirstSaga : IMessage
    {
    }

    public class FirstSagaTimeout : IMessage
    {
    }

    public class StartSecondSaga : IMessage
    {
    }

    public class SecondSagaData : IContainSagaData
    {
        public Guid Id { get; set; }
        public string Originator { get; set; }
        public string OriginalMessageId { get; set; }
    }

    public class FirstSagaData : IContainSagaData
    {
        public Guid Id { get; set; }
        public string Originator { get; set; }
        public string OriginalMessageId { get; set; }
    }
}
