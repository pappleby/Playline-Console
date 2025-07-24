namespace PlaylineConsole
{
    using System;
    using Newtonsoft.Json;

    public class PlaylineCustomConverter : JsonConverter
    {
        public PlaylineCustomConverter()
        {
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is Yarn.Operand op)
            {
                switch (op.ValueCase)
                {
                    case Yarn.Operand.ValueOneofCase.FloatValue: writer.WriteValue(op.FloatValue); break;
                    case Yarn.Operand.ValueOneofCase.BoolValue: writer.WriteValue(op.BoolValue); break;
                    case Yarn.Operand.ValueOneofCase.StringValue: writer.WriteValue(op.StringValue); break;
                    default: writer.WriteNull(); break;
                }
            }
            else if (value is Yarn.Instruction.InstructionTypeOneofCase oo)
            {
                writer.WriteValue(oo.ToString());
            }
            else if (value == null)
            {
                writer.WriteNull();
                return;
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanRead
        {
            get { return false; }
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType.FullName == "Yarn.Operand" || objectType.FullName == "Yarn.Instruction+InstructionTypeOneofCase";
        }
    }
}
