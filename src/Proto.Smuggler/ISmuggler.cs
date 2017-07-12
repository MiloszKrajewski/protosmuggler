namespace Proto.Smuggler
{
	public interface ISmuggler
	{
		byte[] Serialize(object subject);
		object Deserialize(byte[] message);
	}
}
