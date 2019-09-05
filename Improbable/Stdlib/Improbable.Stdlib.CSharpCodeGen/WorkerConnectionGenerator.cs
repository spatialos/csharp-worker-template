using System.Collections.Generic;
using System.Text;
using Improbable.CSharpCodeGen;
using Improbable.Schema.Bundle;

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
                sb.AppendLine(GenerateUpdate(type));
            }

            sb.AppendLine(GenerateComponentCollection(type));

            return sb.ToString();
        }

        private static string GenerateComponentCollection(TypeDescription type)
        {
            var name = Case.CapitalizeNamespace(type.QualifiedName);
            var collectionType = $"global::Improbable.Stdlib.ComponentCollection<global::{name}>";

            return $@"public static {collectionType} CreateComponentCollection()
{{
    return new {collectionType}(ComponentId, Create, ApplyUpdate);
}}";
        }

        private static string GenerateCommands(TypeDescription type, IReadOnlyList<ComponentDefinition.CommandDefinition> commands)
        {
            var componentName = Case.CapitalizeNamespace(type.QualifiedName);
            var text = new StringBuilder();
            var bindingMethods = new StringBuilder();

            var commandIndices = new StringBuilder();
            foreach (var cmd in commands)
            {
                var response = Case.CapitalizeNamespace(cmd.ResponseType);
                var request = Case.CapitalizeNamespace(cmd.RequestType);
                var cmdName = Case.SnakeCaseToPascalCase(cmd.Name);

                commandIndices.AppendLine($"{cmdName} = {cmd.CommandIndex},");

                var boundResponseSender = "";

                // Don't allow workers to send command responses for system commands.
                if (!type.IsRestricted)
                {
                    boundResponseSender = $@"public void Send{cmdName}Response(uint id, {response} response)
{{
    global::{Case.CapitalizeNamespace(type.QualifiedName)}.Send{cmdName}Response(connection, id, response);
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
    return global::{Case.CapitalizeNamespace(type.QualifiedName)}.Send{cmdName}Async(connection, entityId, request, cancellation, timeout, commandParameters);
}}
{boundResponseSender}");
                var responseSender = "";

                // Don't allow workers to send command responses for system commands.
                if (!type.IsRestricted)
                {
                    responseSender = $@"public static void Send{cmdName}Response({WorkerConnectionType} connection, uint id, {response} response)
{{
    var schemaResponse = new global::Improbable.Worker.CInterop.SchemaCommandResponse({componentName}.ComponentId, {cmd.CommandIndex});
    response.ApplyToSchemaObject(schemaResponse.GetObject());

    connection.SendCommandResponse(id, schemaResponse);
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
    var schemaRequest = new global::Improbable.Worker.CInterop.SchemaCommandRequest({componentName}.ComponentId, {cmd.CommandIndex});
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

    connection.Send(entityId, schemaRequest, timeout, commandParameters, Complete, Fail);

    return completion.Task;
}}");
            }

            if (commandIndices.Length > 0)
            {
                text.Append($@"
public enum Commands
{{
{Case.Indent(1, commandIndices.ToString().TrimEnd())}
}}

public static Commands? GetCommandType(global::Improbable.Worker.CInterop.CommandRequestOp request)
{{
    if (request.Request.ComponentId != ComponentId)
    {{
        throw new global::System.InvalidOperationException($""Mismatch of ComponentId (expected {{ComponentId}} but got {{request.Request.ComponentId}}"");
    }}

    if (!request.Request.SchemaData.HasValue)
    {{
        return null;
    }}

    return (Commands)request.Request.SchemaData.Value.GetCommandIndex();
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
{Case.Indent(1, bindingMethods.ToString().TrimEnd())}
}}

public static CommandSenderBinding Bind({WorkerConnectionType} connection, {Types.EntityIdType} entityId)
{{
    return new CommandSenderBinding(connection, entityId);
}}
");
            }

            return text.ToString();
        }

        public static string GenerateUpdate(TypeDescription type)
        {
            var typeName = Case.GetPascalCaseNameFromTypeName(type.QualifiedName);
            var typeNamespace = Case.GetPascalCaseNamespaceFromTypeName(type.QualifiedName);

            var update = $"global::{typeNamespace}.{typeName}.Update";

            return $@"public static void SendUpdate({WorkerConnectionType} connection, {Types.EntityIdType} entityId, {update} update, global::Improbable.Worker.CInterop.UpdateParameters? updateParams = null)
{{
    connection.SendComponentUpdate(entityId.Value, update.ToSchemaUpdate(), updateParams);
}}
";
        }
    }
}
