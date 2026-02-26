#nullable enable
using System;
using Scynapse.Runtime;

namespace Scynapse.Placement.Repartitioning;

internal interface IMessageStatisticsSink
{
    Action<Message>? GetMessageObserver();
}

internal sealed class NoOpMessageStatisticsSink : IMessageStatisticsSink
{
    public Action<Message>? GetMessageObserver() => null;
}