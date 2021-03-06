using System.Collections.Generic;
using System.Text;
using Improbable.CSharpCodeGen;
using Improbable.Schema.Bundle;
using static Improbable.CSharpCodeGen.Case;

namespace Improbable.Stdlib.CSharpCodeGen
{
    public class StdlibGenerator : ICodeGenerator
    {
        private const string WorkerConnectionType = "global::Improbable.Stdlib.WorkerConnection";
        private const string CancellationTokenType = "global::System.Threading.CancellationToken";

        private readonly Bundle bundle;

        public StdlibGenerator(Bundle bundle)
        {
            this.bundle = bundle;
        }

        public string Generate(TypeDescription type)
        {
            if (!type.ComponentId.HasValue)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();

            sb.AppendLine(GenerateCommands(type, bundle.Components[type.QualifiedName].Commands));
            if (!type.IsRestricted)
            {
                // Workers can't construct or send updates for restricted components.
                sb.AppendLine(GenerateSendUpdate(type));
                sb.AppendLine(GenerateEventProcessors(type));
            }

            sb.AppendLine(GenerateComponentCollection(type));

            return sb.ToString();
        }

        private static string GenerateComponentCollection(TypeDescription type)
        {
            var collectionType = $"global::Improbable.Stdlib.ComponentCollection<{type.Fqn()}>";

            return $@"public static {collectionType} CreateComponentCollection()
{{
    return new {collectionType}(ComponentId, Create, ApplyUpdate);
}}";
        }

        private static string GenerateEventProcessors(TypeDescription type)
        {
            if (type.Events.IsEmpty)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (var evt in type.Events)
            {
                var name = SnakeCaseToPascalCase(evt.Name);
                var events = $"global::Improbable.Stdlib.EventProcessor<{evt.Fqn()}>";

                sb.AppendLine($@"public static {events} Create{name}EventProcessor()
{{
    return new {events}(ComponentId, TryGetEvents);
}}");
            }

            return sb.ToString();
        }

        private static string GenerateCommands(TypeDescription type, IEnumerable<ComponentDefinition.CommandDefinition> commands)
        {
            var text = new StringBuilder();
            var bindingMethods = new StringBuilder();

            var commandIndices = new StringBuilder();
            foreach (var cmd in commands)
            {
                var (request, response) = cmd.InnerFqns();
                var cmdName = cmd.Name();

                commandIndices.AppendLine($"{cmdName} = {cmd.CommandIndex},");

                var boundResponseSender = "";

                // Don't allow workers to send command responses for system commands.
                if (!type.IsRestricted)
                {
                    boundResponseSender = $@"public void Send{cmdName}Response(long id, {response} response)
{{
    {type.Fqn()}.Send{cmdName}Response(connection, id, response);
}}";
                }

                bindingMethods.AppendLine($@"
/// <summary>
/// Sends a command to the worker that is authoritative over the bound entityId./>.
/// </summary>
/// <param name=""request""> The request payload.</param>
/// <param name=""cancellation""> A token used to mark the task as cancelled.
/// Cancelling will NOT cancel the sending of the command to the authoritative worker; it WILL be processed by the runtime and the target worker.
/// It only marks the task as cancelled locally. Use this for flow control or cleanup of state.
/// </param>
/// <param name=""timeout""> The amount of time that must pass before a command response is considered ""timed out"".</param>
/// <param name=""commandParameters""> Options used to configure how the command is sent. </param>
/// <returns> A Task containing response payload. </returns>
public global::System.Threading.Tasks.Task<{response}> Send{cmdName}Async({request} request, {CancellationTokenType} cancellation = default, uint? timeout = null, global::Improbable.Worker.CInterop.CommandParameters? commandParameters = null)
{{
    return {type.Fqn()}.Send{cmdName}Async(connection, entityId, request, cancellation, timeout, commandParameters);
}}
{boundResponseSender}");
                var responseSender = "";

                // Don't allow workers to send command responses for system commands.
                if (!type.IsRestricted)
                {
                    responseSender = $@"public static void Send{cmdName}Response({WorkerConnectionType} connection, long id, {response} response)
{{
    var schemaResponse = global::Improbable.Worker.CInterop.SchemaCommandResponse.Create();
    response.ApplyToSchemaObject(schemaResponse.GetObject());

    connection.SendCommandResponse(id, {type.Fqn()}.ComponentId, {cmd.CommandIndex}, schemaResponse);
}}
";
                }

                text.AppendLine($@"{responseSender}
/// <summary>
/// Sends a command to the worker that is authoritative over <paramref name=""entityId""/>.
/// </summary>
/// <param name=""connection""> </param>
/// <param name=""entityId""> </param>
/// <param name=""request""> The request payload.</param>
/// <param name=""cancellation""> A token used to mark the task as cancelled.
/// Cancelling will NOT cancel the sending of the command to the authoritative worker; it WILL be processed by the runtime and the target worker.
/// It only marks the task as cancelled locally. Use this for flow control or cleanup of state.
/// </param>
/// <param name=""timeout""> The amount of time that must pass before a command response is considered ""timed out"".</param>
/// <param name=""commandParameters""> Options used to configure how the command is sent. </param>
/// <param name=""taskOptions""> Options that control how the task is scheduled and executed. </param>
/// <returns> A Task containing response payload. </returns>
public static global::System.Threading.Tasks.Task<{response}> Send{cmdName}Async({WorkerConnectionType} connection,
                                                                                 {Types.EntityIdType} entityId,
                                                                                 {request} request,
                                                                                 {CancellationTokenType} cancellation = default,
                                                                                 uint? timeout = null,
                                                                                 global::Improbable.Worker.CInterop.CommandParameters? commandParameters = null,
                                                                                 global::System.Threading.Tasks.TaskCreationOptions taskOptions = global::System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously)
{{
    var schemaRequest = global::Improbable.Worker.CInterop.SchemaCommandRequest.Create();
    request.ApplyToSchemaObject(schemaRequest.GetObject());

    var completion = new global::System.Threading.Tasks.TaskCompletionSource<{response}>(taskOptions);
    if (cancellation.CanBeCanceled)
    {{
        cancellation.Register(() => completion.TrySetCanceled(cancellation));
    }}

    void Complete(global::Improbable.Stdlib.WorkerConnection.CommandResponses r)
    {{
        var result = new {response}(r.UserCommand.Response.SchemaData.Value.GetObject());
        completion.TrySetResult(result);
    }}

    void Fail(global::Improbable.Worker.CInterop.StatusCode code, string message)
    {{
        completion.TrySetException(new global::Improbable.Stdlib.CommandFailedException(code, message));
    }}

    connection.Send(entityId, {type.Fqn()}.ComponentId, {cmd.CommandIndex}, schemaRequest, timeout, commandParameters, Complete, Fail);

    return completion.Task;
}}");
            }

            if (commandIndices.Length > 0)
            {
                text.Append($@"
public enum Commands
{{
{Indent(1, commandIndices.ToString().TrimEnd())}
}}

public static Commands GetCommandType(global::Improbable.Worker.CInterop.CommandRequestOp request)
{{
    if (request.Request.ComponentId != ComponentId)
    {{
        throw new global::System.InvalidOperationException($""Mismatch of ComponentId (expected {{ComponentId}} but got {{request.Request.ComponentId}}"");
    }}

    return (Commands) request.Request.CommandIndex;
}}

public readonly struct CommandSenderBinding
{{
    private readonly {WorkerConnectionType} connection;
    private readonly {Types.EntityIdType} entityId;

    public CommandSenderBinding({WorkerConnectionType} connection, {Types.EntityIdType} entityId)
    {{
        this.connection = connection;
        this.entityId = entityId;
    }}
{Indent(1, bindingMethods.ToString().TrimEnd())}
}}

public static CommandSenderBinding Bind({WorkerConnectionType} connection, {Types.EntityIdType} entityId)
{{
    return new CommandSenderBinding(connection, entityId);
}}
");
            }

            return text.ToString();
        }

        public static string GenerateSendUpdate(TypeDescription type)
        {
            return $@"public static void SendUpdate({WorkerConnectionType} connection, {Types.EntityIdType} entityId, {$"{type.Fqn()}.Update"} update, global::Improbable.Worker.CInterop.UpdateParameters? updateParams = null)
{{
    connection.SendComponentUpdate(entityId.Value, ComponentId, update.ToSchemaUpdate(), updateParams);
}}
";
        }
    }
}
