using System;
using Frends.AS4.Send.Definitions;

namespace Frends.AS4.Send.Helpers;

internal static class ErrorHandler
{
    internal static Result Handle(Exception exception, bool throwOnFailure, string errorMessageOnFailure = "")
    {
        if (throwOnFailure)
        {
            if (string.IsNullOrEmpty(errorMessageOnFailure))
                throw new Exception(exception.Message, exception);

            throw new Exception(errorMessageOnFailure, exception);
        }

        var errorMessage = !string.IsNullOrEmpty(errorMessageOnFailure)
            ? $"{errorMessageOnFailure}: {exception.Message}"
            : exception.Message;

        return new Result
        {
            Success = false,
            Error = new Error { Message = errorMessage, AdditionalInfo = exception },
        };
    }
}
