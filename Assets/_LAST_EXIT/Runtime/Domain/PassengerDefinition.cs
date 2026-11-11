using System;

namespace LastExit.Domain
{
    public class PassengerDefinition
    {
        public const string HumanKind = "human";
        public const string EchoKind = "echo";

        public PassengerDefinition(string id, string route, string kind = HumanKind)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Passenger ID is required.", nameof(id));
            }

            if (route != "R" && route != "T" && route != "B" && route != "G")
            {
                throw new ArgumentException("Unknown passenger route.", nameof(route));
            }

            if (kind != HumanKind && kind != EchoKind)
            {
                throw new ArgumentException("Unknown passenger kind.", nameof(kind));
            }

            Id = id;
            Route = route;
            Kind = kind;
        }

        public string Id { get; }
        public string Route { get; }
        public string Kind { get; }
    }
}
