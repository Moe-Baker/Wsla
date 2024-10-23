using Wsla.Serialization;

INetworkStream stream = default;

NetworkSerializer.Write(new Data(), ref stream);
NetworkSerializer.Write(new Data[0], ref stream);

while (true)
    Console.ReadKey();

[NetworkBlittable]
struct Data
{
    public float X, Y, Z;
}