using System;
using System.Buffers;
using Orleans.Serialization.Buffers;
using Orleans.Serialization.Cloning;
using Orleans.Serialization.WireProtocol;

namespace Orleans.Serialization.Codecs
{
    /// <summary>
    /// Serializer for <see cref="EventArgs"/>.
    /// </summary>
    /// <remarks>
    /// Since <see cref="EventArgs"/> has no state, this codec serializes nothing
    /// and always returns <see cref="EventArgs.Empty"/> on deserialization.
    /// </remarks>
    [RegisterSerializer]
    public sealed class EventArgsCodec : IFieldCodec<EventArgs>
    {
        /// <inheritdoc />
        public void WriteField<TBufferWriter>(ref Writer<TBufferWriter> writer, uint fieldIdDelta, Type expectedType, EventArgs value)
            where TBufferWriter : IBufferWriter<byte>
        {
            // EventArgs has no state, so we just write a null reference marker if null,
            // or a simple type header if not null (the type itself carries all the info)
            if (value is null)
            {
                ReferenceCodec.WriteNullReference(ref writer, fieldIdDelta);
                return;
            }

            // Write a simple marker - EventArgs.Empty is a singleton with no data
            ReferenceCodec.MarkValueField(writer.Session);
            writer.WriteFieldHeader(fieldIdDelta, expectedType, typeof(EventArgs), WireType.VarInt);
            writer.WriteVarUInt32(0); // Write a zero to indicate "empty" EventArgs
        }

        /// <inheritdoc />
        public EventArgs ReadValue<TInput>(ref Reader<TInput> reader, Field field)
        {
            if (field.WireType == WireType.Reference)
            {
                return ReferenceCodec.ReadReference<EventArgs, TInput>(ref reader, field);
            }

            // Read and discard the marker value
            reader.ReadVarUInt32();

            var result = EventArgs.Empty;
            ReferenceCodec.RecordObject(reader.Session, result);
            return result;
        }
    }

    /// <summary>
    /// Copier for <see cref="EventArgs"/>.
    /// </summary>
    [RegisterCopier]
    public sealed class EventArgsCopier : IDeepCopier<EventArgs>
    {
        /// <inheritdoc />
        public EventArgs DeepCopy(EventArgs input, CopyContext context)
        {
            // EventArgs.Empty is a singleton with no state, so we can return it directly
            // For any EventArgs instance, we return EventArgs.Empty since there's no state to copy
            return EventArgs.Empty;
        }
    }
}
