﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using Microsoft.Extensions.DependencyInjection;
using Orleans.CodeGeneration;
using Orleans.Concurrency;
using Orleans.Serialization;
using Squidex.Infrastructure.Json;

#pragma warning disable IDE0060 // Remove unused parameter

namespace Squidex.Infrastructure.Orleans
{
    [Immutable]
    public readonly struct J<T>
    {
        public T Value { get; }

        public J(T value)
        {
            Value = value;
        }

        public static implicit operator T(J<T> value)
        {
            return value.Value;
        }

        public static implicit operator J<T>(T d)
        {
            return new J<T>(d);
        }

        public override string ToString()
        {
            return Value?.ToString() ?? string.Empty;
        }

        public static Task<J<T>> AsTask(T value)
        {
            return Task.FromResult<J<T>>(value);
        }

        [CopierMethod]
        public static object? Copy(object? input, ICopyContext? context)
        {
            return input;
        }

        [SerializerMethod]
        public static void Serialize(object? input, ISerializationContext context, Type? expected)
        {
            var stream = new StreamWriterWrapper(context.StreamWriter);

            GetSerializer(context).Serialize(input, stream);
        }

        [DeserializerMethod]
        public static object? Deserialize(Type expected, IDeserializationContext context)
        {
            var stream = new StreamReaderWrapper(context.StreamReader);

            return GetSerializer(context).Deserialize<object>(stream, expected);
        }

        private static IJsonSerializer GetSerializer(ISerializerContext context)
        {
            try
            {
                return context?.ServiceProvider?.GetRequiredService<IJsonSerializer>() ?? J.DefaultSerializer;
            }
            catch
            {
                return J.DefaultSerializer;
            }
        }
    }
}
