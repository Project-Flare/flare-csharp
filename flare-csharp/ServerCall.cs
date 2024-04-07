using Grpc.Core;

namespace flare_csharp
{
    internal static class ServerCall<T>
    {
        /// <summary>
        /// The task of the unary is not completed successfully.
        /// </summary>
        public class TaskNotCompletedSuccessfullyException : Exception { }

        /// <summary>
        /// Take the unary gRPC call and complete the set task of the call.
        /// </summary>
        /// <param name="call">Specific gRPC unary call.</param>
        /// <returns>
        /// Result of the given gRPC unary call
        /// </returns>
        /// <exception cref="TaskNotCompletedSuccessfullyException">
        /// Thrown when the task of the unary is not completed successfully.
        /// </exception>
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
