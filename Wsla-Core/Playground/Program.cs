using Wsla.Serialization;

var stream = new NetworkStream(512);

NetworkSerializer.WriteValue(new Data(), ref stream);
NetworkSerializer.WriteValue(new Data[0], ref stream);

while (true)
    Console.ReadKey();

[NetworkBlittable]
struct Data
{
    public float X, Y, Z;
}