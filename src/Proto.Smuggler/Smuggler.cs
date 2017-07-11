using System;
using Google.Protobuf;

namespace Proto.Smuggler
{
	public class Smuggler
	{
		private static Func<object, byte[]> _serialize;
		private static Func<byte[], object> _deserialize;

		public static void Use(
			Func<object, byte[]> serialize,
			Func<byte[], object> deserialize)
		{
			_serialize = serialize;
			_deserialize = deserialize;
		}

		public static void Use(ISmuggler smuggler)
		{
			_serialize = smuggler.Serialize;
			_deserialize = smuggler.Deserialize;
		}

		public static Contraband Conceal(object message) =>
			new Contraband { Payload = ByteString.CopyFrom(_serialize(message)) };

		public static object Reveal(object message) =>
			message is Contraband contraband
				? _deserialize(contraband.Payload.ToByteArray())
				: message;

		private static object TryReveal(Contraband contraband)
		{
			try
			{
				return _deserialize(contraband.Payload.ToByteArray());
			}
			catch
			{
				return contraband;
			}
		}

		public static object TryReveal(object message) =>
			message is Contraband contraband ? TryReveal(contraband) : message;
	}

	public interface ISmuggler
	{
		byte[] Serialize(object subject);
		object Deserialize(byte[] message);
	}
}
