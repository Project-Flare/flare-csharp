using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace flare_csharp
{
    internal static class ServerCall<T>
    {
        public class TaskNotCompletedSuccessfullyException : Exception { }
        public static async Task<T> FulfilUnaryCallAsync(AsyncUnaryCall<T> call)
        {
            Task<T> task = call.ResponseAsync;
            await task;
            if (!task.IsCompletedSuccessfully)
                throw new TaskNotCompletedSuccessfullyException();

            return task.Result;
        }
    }
}
