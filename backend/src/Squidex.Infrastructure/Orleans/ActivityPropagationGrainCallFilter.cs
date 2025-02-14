﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using System.Diagnostics;
using Orleans;
using Orleans.Runtime;

#pragma warning disable MA0048 // File name must match type name

namespace Squidex.Infrastructure.Orleans
{
    public abstract class ActivityPropagationGrainCallFilter
    {
        public const string ActivityNameIn = "Orleans.Runtime.GrainCall.In";
        public const string ActivityNameOut = "Orleans.Runtime.GrainCall.Out";
        protected const string TraceParentHeaderName = "traceparent";
        protected const string TraceStateHeaderName = "tracestate";

        protected static async Task ProcessNewActivity(IGrainCallContext context, string activityName, ActivityKind activityKind, ActivityContext activityContext)
        {
            ActivityTagsCollection? tags = null;

            if (Telemetry.Activities.HasListeners())
            {
                tags = new ActivityTagsCollection
                {
                    { "net.peer.name", context.Grain?.ToString() },
                    { "rpc.method", context.InterfaceMethod?.Name },
                    { "rpc.service", context.InterfaceMethod?.DeclaringType?.FullName },
                    { "rpc.system", "orleans" }
                };
            }

            using (var activity = Telemetry.Activities.StartActivity(activityName, activityKind, activityContext, tags))
            {
                if (activity is not null)
                {
                    RequestContext.Set(TraceParentHeaderName, activity.Id);
                }

                try
                {
                    await context.Invoke();

                    if (activity is not null && activity.IsAllDataRequested)
                    {
                        activity.SetTag("status", "Ok");
                    }
                }
                catch (Exception e)
                {
                    if (activity is not null && activity.IsAllDataRequested)
                    {
                        // Exception attributes from https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/exceptions.md
                        activity.SetTag("exception.type", e.GetType().FullName);
                        activity.SetTag("exception.message", e.Message);
                        activity.SetTag("exception.stacktrace", e.StackTrace);
                        activity.SetTag("exception.escaped", true);
                        activity.SetTag("status", "Error");
                    }

                    throw;
                }
            }
        }
    }

    public sealed class ActivityPropagationOutgoingGrainCallFilter : ActivityPropagationGrainCallFilter, IOutgoingGrainCallFilter
    {
        public Task Invoke(IOutgoingGrainCallContext context)
        {
            if (Activity.Current != null)
            {
                return ProcessCurrentActivity(context);
            }

            return ProcessNewActivity(context, ActivityNameOut, ActivityKind.Client, default);
        }

        private static Task ProcessCurrentActivity(IOutgoingGrainCallContext context)
        {
            var currentActivity = Activity.Current;

            if (currentActivity is not null && currentActivity.IdFormat == ActivityIdFormat.W3C)
            {
                RequestContext.Set(TraceParentHeaderName, currentActivity.Id);

                if (currentActivity.TraceStateString is not null)
                {
                    RequestContext.Set(TraceStateHeaderName, currentActivity.TraceStateString);
                }
            }

            return context.Invoke();
        }
    }

    public sealed class ActivityPropagationIncomingGrainCallFilter : ActivityPropagationGrainCallFilter, IIncomingGrainCallFilter
    {
        public Task Invoke(IIncomingGrainCallContext context)
        {
            var traceParent = RequestContext.Get(TraceParentHeaderName) as string;
            var traceState = RequestContext.Get(TraceStateHeaderName) as string;
            var parentContext = default(ActivityContext);

            if (traceParent is not null)
            {
                parentContext = ActivityContext.Parse(traceParent, traceState);
            }

            return ProcessNewActivity(context, ActivityNameIn, ActivityKind.Server, parentContext);
        }
    }
}
