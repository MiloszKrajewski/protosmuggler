using System.Text;
using Newtonsoft.Json;

namespace Proto.Smuggler.Json
{
	public class JsonSmuggler: ISmuggler
	{
		private readonly JsonSerializerSettings _settings;

		public JsonSmuggler()
		{
			_settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };
		}

		public JsonSmuggler(JsonSerializerSettings settings)
		{
			_settings = settings;
		}

		public byte[] Serialize(object subject) =>
			Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(subject, typeof(object), _settings));

		public object Deserialize(byte[] message) =>
			JsonConvert.DeserializeObject(Encoding.UTF8.GetString(message), _settings);
	}
}
