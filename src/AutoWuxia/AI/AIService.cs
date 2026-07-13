using System.IO;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutoWuxia.Core;

namespace AutoWuxia.AI;

public class AIService
{
    private readonly HttpClient _httpClient = new();
    private AIConfig _config;

    public AIService(AIConfig config)
    {
        _config = config;
        _httpClient.Timeout = TimeSpan.FromSeconds(120);
    }

    public void UpdateConfig(AIConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// 拼接 chat/completions 端点。兼容用户填写带 /v1 或不带 /v1 的 endpoint:
    /// 已以 /v1 结尾则直接接 /chat/completions,否则补 /v1/chat/completions。
    /// </summary>
    private string GetChatEndpoint()
    {
        var baseUrl = _config.ApiEndpoint.TrimEnd('/');
        return baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? $"{baseUrl}/chat/completions"
            : $"{baseUrl}/v1/chat/completions";
    }

    /// <summary>
    /// 序列化请求体。小米reasoning模型(mimo-v2.5/pro)默认开启思考链,
    /// 通过 thinking.type=disabled 关闭以节省token、加快响应(content仍有正文)。其他API忽略此参数。
    /// </summary>
    private string SerializeRequest<T>(T request)
    {
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = false });
        if (_config.ApiEndpoint.Contains("xiaomimimo", StringComparison.OrdinalIgnoreCase))
        {
            var node = JsonNode.Parse(json);
            if (node != null)
            {
                node["thinking"] = new JsonObject { ["type"] = "disabled" };
                json = node.ToJsonString();
            }
        }
        return json;
    }

    public async Task<string?> ChatAsync(string systemPrompt, string userMessage)
    {
        if (string.IsNullOrEmpty(_config.ApiKey))
        {
            GameLogger.AI("[错误] API Key 未配置");
            return "[AI未配置API Key，请在设置中配置]";
        }

        try
        {
            var request = new
            {
                model = _config.Model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userMessage }
                },
                temperature = _config.Temperature,
                max_tokens = _config.MaxTokens
            };

            var json = SerializeRequest(request);

            // 记录请求日志
            var endpoint = GetChatEndpoint();
            GameLogger.AI($"[请求] URL: {endpoint}");
            GameLogger.AI($"[请求] Model: {_config.Model}, Temperature: {_config.Temperature}, MaxTokens: {_config.MaxTokens}");
            GameLogger.AI($"[请求] SystemPrompt({systemPrompt.Length}字): {Truncate(systemPrompt, 200)}");
            GameLogger.AI($"[请求] UserMessage({userMessage.Length}字): {Truncate(userMessage, 300)}");

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
            httpRequest.Content = content;
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await _httpClient.SendAsync(httpRequest);
            sw.Stop();

            var responseJson = await response.Content.ReadAsStringAsync();

            GameLogger.AI($"[响应] 状态码: {(int)response.StatusCode} {response.StatusCode}, 耗时: {sw.ElapsedMilliseconds}ms");
            GameLogger.AI($"[响应] 原始内容({responseJson.Length}字): {Truncate(responseJson, 500)}");

            if (!response.IsSuccessStatusCode)
            {
                GameLogger.AI($"[错误] API调用失败: {response.StatusCode} - {responseJson}");
                return $"[AI调用失败: {response.StatusCode}]";
            }

            using var doc = JsonDocument.Parse(responseJson);
            var choices = doc.RootElement.GetProperty("choices");
            if (choices.GetArrayLength() > 0)
            {
                var message = choices[0].GetProperty("message");
                var reply = message.GetProperty("content").GetString();
                // deepseek-v4-flash 等 reasoning 模型有时把正文放在 reasoning_content 而 content 为空,
                // 此时回退到 reasoning_content(内含思考散文 + JSON, 由下游 ChatStructuredAsync 提取)
                if (string.IsNullOrEmpty(reply)
                    && message.TryGetProperty("reasoning_content", out var rcEl)
                    && rcEl.ValueKind == JsonValueKind.String)
                {
                    reply = rcEl.GetString();
                    GameLogger.AI($"[回复] content为空,回退 reasoning_content({reply?.Length ?? 0}字): {Truncate(reply ?? "", 500)}");
                }
                else
                {
                    GameLogger.AI($"[回复] ({reply?.Length ?? 0}字): {Truncate(reply ?? "", 500)}");
                }
                return reply;
            }

            GameLogger.AI("[错误] API返回了空的choices");
            return "[AI未返回结果]";
        }
        catch (TaskCanceledException)
        {
            GameLogger.AI("[错误] API请求超时(30秒)");
            return "[AI请求超时]";
        }
        catch (Exception ex)
        {
            GameLogger.AI($"[异常] {ex.GetType().Name}: {ex.Message}");
            return $"[AI调用异常: {ex.Message}]";
        }
    }

    public async Task<T?> ChatStructuredAsync<T>(string systemPrompt, string userMessage) where T : class
    {
        var result = await ChatAsync(systemPrompt, userMessage);

        // 检测错误消息
        if (result == null || result.StartsWith("["))
        {
            GameLogger.AI($"[结构化] ChatAsync返回错误或null: {result}");
            return null;
        }

        try
        {
            var json = ExtractJson(result);
            if (json != null)
            {
                GameLogger.AI($"[结构化] 提取JSON: {Truncate(json, 300)}");
                var parsed = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                GameLogger.AI($"[结构化] 反序列化成功: {typeof(T).Name}");
                return parsed;
            }
            GameLogger.AI($"[结构化] 未找到JSON对象，原始回复: {Truncate(result, 300)}");
        }
        catch (Exception ex)
        {
            GameLogger.AI($"[结构化] JSON解析失败: {ex.Message}, 原始内容: {Truncate(result, 300)}");
        }
        return null;
    }

    /// <summary>
    /// 从文本中提取 JSON 对象。优先取第一个{ 到最后一个}(兼容嵌套);
    /// 若解析失败,再从后往前找最后一个可解析的{...}(应对 reasoning_content 前面带含{ 的思考散文)。
    /// </summary>
    private static string? ExtractJson(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var firstStart = text.IndexOf('{');
        var lastEnd = text.LastIndexOf('}');
        if (firstStart >= 0 && lastEnd > firstStart)
        {
            var candidate = text.Substring(firstStart, lastEnd - firstStart + 1);
            try
            {
                using var doc = JsonDocument.Parse(candidate);
                return candidate;
            }
            catch { }
        }
        // 回退: 从后往前逐个尝试 {...},直到找到可解析的
        var end = lastEnd;
        while (end >= 0)
        {
            var start = text.LastIndexOf('{', end);
            if (start < 0) break;
            var candidate = text.Substring(start, end - start + 1);
            try
            {
                using var doc = JsonDocument.Parse(candidate);
                return candidate;
            }
            catch
            {
                end = text.LastIndexOf('}', start - 1);
            }
        }
        return null;
    }

    // ── 流式输出支持 ──

    /// <summary>
    /// 流式聊天:逐块回调 <paramref name="onChunk"/>,用于结束画面打字机式输出。
    /// 失败/未配置/取消时静默返回(不抛异常),由调用方走降级文案。
    /// reasoning 模型若全程不吐 content,回退输出 reasoning_content(与 ChatAsync 一致)。
    /// </summary>
    public async Task ChatStreamAsync(string systemPrompt, string userMessage, Action<string> onChunk, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_config.ApiKey))
        {
            GameLogger.AI("[流式错误] API Key 未配置");
            return;
        }

        try
        {
            var request = new
            {
                model = _config.Model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userMessage }
                },
                temperature = _config.Temperature,
                max_tokens = _config.MaxTokens,
                stream = true
            };

            var json = SerializeRequest(request);
            var endpoint = GetChatEndpoint();
            GameLogger.AI($"[流式请求] URL: {endpoint}, Model: {_config.Model}");

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);

            using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                GameLogger.AI($"[流式错误] {response.StatusCode}: {Truncate(errBody, 300)}");
                return;
            }

            using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var reader = new StreamReader(stream);

            var reasoning = new StringBuilder();
            var hasContent = false;
            while (!reader.EndOfStream)
            {
                ct.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (string.IsNullOrEmpty(line)) continue;
                if (!line.StartsWith("data:")) continue;
                var data = line["data:".Length..].Trim();
                if (data == "[DONE]") break;
                if (data.Length == 0) continue;

                try
                {
                    using var doc = JsonDocument.Parse(data);
                    if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                        continue;
                    var delta = choices[0].GetProperty("delta");
                    if (delta.TryGetProperty("content", out var cEl) && cEl.ValueKind == JsonValueKind.String)
                    {
                        var piece = cEl.GetString();
                        if (!string.IsNullOrEmpty(piece))
                        {
                            hasContent = true;
                            onChunk(piece);
                        }
                    }
                    // reasoning 模型可能先吐 reasoning_content(思考链),正文 content 随后;仅做兜底累积
                    if (delta.TryGetProperty("reasoning_content", out var rcEl) && rcEl.ValueKind == JsonValueKind.String)
                    {
                        var piece = rcEl.GetString();
                        if (!string.IsNullOrEmpty(piece)) reasoning.Append(piece);
                    }
                }
                catch (JsonException)
                {
                    // 单行解析失败,跳过继续读下一行
                }
            }

            // 全程未吐 content(部分模型只给 reasoning_content),整体回退输出
            if (!hasContent && reasoning.Length > 0)
            {
                GameLogger.AI($"[流式] content 全空,回退 reasoning_content({reasoning.Length}字)");
                onChunk(reasoning.ToString());
            }
            GameLogger.AI("[流式] 完成");
        }
        catch (OperationCanceledException)
        {
            GameLogger.AI("[流式错误] 取消/超时");
        }
        catch (Exception ex)
        {
            GameLogger.AI($"[流式异常] {ex.GetType().Name}: {ex.Message}");
        }
    }

    // ── Tool Calling 支持 ──

    /// <summary>
    /// 支持 OpenAI 兼容的 tool calling 的聊天方法
    /// 用于 Agent 循环，支持多轮对话（含 tool_call/tool_result）
    /// </summary>
    public async Task<AgentResponse> ChatWithToolsAsync(
        string systemPrompt,
        List<ChatMessage> messages,
        List<ToolDefinition> tools,
        string? modelOverride = null)
    {
        if (string.IsNullOrEmpty(_config.ApiKey))
        {
            GameLogger.AI("[错误] API Key 未配置");
            return new AgentResponse { Content = "[AI未配置API Key]", FinishReason = "error" };
        }

        try
        {
            var model = modelOverride ?? _config.Model;
            var endpoint = GetChatEndpoint();

            // 构建消息数组：system + 历史消息
            var apiMessages = new List<object>
            {
                new { role = "system", content = systemPrompt }
            };
            foreach (var msg in messages)
            {
                if (msg.Role == "tool")
                {
                    apiMessages.Add(new { role = "tool", content = msg.Content ?? "", tool_call_id = msg.ToolCallId ?? "" });
                }
                else if (msg.ToolCalls != null && msg.ToolCalls.Count > 0)
                {
                    // assistant message with tool_calls
                    var tcArray = msg.ToolCalls.Select(tc => new
                    {
                        id = tc.Id,
                        type = "function",
                        function = new { name = tc.FunctionName, arguments = tc.FunctionArguments }
                    }).ToArray();
                    apiMessages.Add(new { role = "assistant", content = msg.Content ?? "", tool_calls = tcArray });
                }
                else
                {
                    apiMessages.Add(new { role = msg.Role, content = msg.Content ?? "" });
                }
            }

            var toolDefs = tools.Select(t => new
            {
                type = "function",
                function = new
                {
                    name = t.Name,
                    description = t.Description,
                    parameters = t.Parameters
                }
            }).ToArray();

            var request = new
            {
                model,
                messages = apiMessages,
                tools = toolDefs,
                tool_choice = "auto",
                temperature = 0.7,
                max_tokens = 4000
            };

            var json = SerializeRequest(request);

            GameLogger.AI($"[Agent请求] URL: {endpoint}, Model: {model}");
            GameLogger.AI($"[Agent请求] Messages: {messages.Count}, Tools: {tools.Count}");

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
            httpRequest.Content = content;
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await _httpClient.SendAsync(httpRequest);
            sw.Stop();

            var responseJson = await response.Content.ReadAsStringAsync();
            GameLogger.AI($"[Agent响应] 状态码: {(int)response.StatusCode}, 耗时: {sw.ElapsedMilliseconds}ms");

            if (!response.IsSuccessStatusCode)
            {
                GameLogger.AI($"[Agent错误] {response.StatusCode}: {Truncate(responseJson, 500)}");
                return new AgentResponse { Content = $"[API调用失败: {response.StatusCode}]", FinishReason = "error" };
            }

            using var doc = JsonDocument.Parse(responseJson);
            var choices = doc.RootElement.GetProperty("choices");
            if (choices.GetArrayLength() == 0)
            {
                return new AgentResponse { Content = "[AI返回空结果]", FinishReason = "error" };
            }

            var choice = choices[0];
            var message = choice.GetProperty("message");
            var finishReason = choice.GetProperty("finish_reason").GetString() ?? "";

            var agentResp = new AgentResponse { FinishReason = finishReason };

            // 解析 content
            if (message.TryGetProperty("content", out var contentEl) && contentEl.ValueKind == JsonValueKind.String)
            {
                agentResp.Content = contentEl.GetString() ?? "";
            }

            // 解析 tool_calls
            if (message.TryGetProperty("tool_calls", out var toolCallsEl) && toolCallsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var tc in toolCallsEl.EnumerateArray())
                {
                    var tcId = tc.GetProperty("id").GetString() ?? "";
                    var func = tc.GetProperty("function");
                    var funcName = func.GetProperty("name").GetString() ?? "";
                    var funcArgs = func.GetProperty("arguments").GetString() ?? "{}";

                    agentResp.ToolCalls.Add(new ToolCall
                    {
                        Id = tcId,
                        FunctionName = funcName,
                        FunctionArguments = funcArgs
                    });
                }
                GameLogger.AI($"[Agent] 返回 {agentResp.ToolCalls.Count} 个工具调用");
            }

            // reasoning 模型有时把正文放 reasoning_content 而 content 为空(仅在无工具调用时回退,避免干扰 tool_calls 流程)
            if (string.IsNullOrEmpty(agentResp.Content) && !agentResp.HasToolCalls
                && message.TryGetProperty("reasoning_content", out var rcEl)
                && rcEl.ValueKind == JsonValueKind.String)
            {
                agentResp.Content = rcEl.GetString() ?? "";
                GameLogger.AI($"[Agent] content为空,回退 reasoning_content({agentResp.Content.Length}字)");
            }

            GameLogger.AI($"[Agent] FinishReason: {finishReason}, Content长度: {agentResp.Content?.Length ?? 0}");
            if (agentResp.Content != null)
                GameLogger.AI($"[Agent] Content: {Truncate(agentResp.Content, 300)}");

            return agentResp;
        }
        catch (TaskCanceledException)
        {
            GameLogger.AI("[Agent错误] 请求超时");
            return new AgentResponse { Content = "[AI请求超时]", FinishReason = "error" };
        }
        catch (Exception ex)
        {
            GameLogger.AI($"[Agent异常] {ex.GetType().Name}: {ex.Message}");
            return new AgentResponse { Content = $"[AI调用异常: {ex.Message}]", FinishReason = "error" };
        }
    }

    private static string Truncate(string text, int maxLen)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= maxLen ? text : text.Substring(0, maxLen) + "...(截断)";
    }
}

// ── Agent 相关类型 ──

/// <summary>
/// 聊天消息（支持多轮对话，含 tool_call/tool_result）
/// </summary>
public class ChatMessage
{
    public string Role { get; set; } = "user";  // system/user/assistant/tool
    public string? Content { get; set; }
    public List<ToolCall>? ToolCalls { get; set; }
    public string? ToolCallId { get; set; }  // tool message 用
}

/// <summary>
/// 工具定义（OpenAI 兼容 function calling）
/// </summary>
public class ToolDefinition
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>
    /// JSON Schema 对象（匿名类型或 Dictionary）
    /// </summary>
    public object Parameters { get; set; } = new { type = "object", properties = new { } };
}

/// <summary>
/// AI 返回的工具调用
/// </summary>
public class ToolCall
{
    public string Id { get; set; } = "";
    public string FunctionName { get; set; } = "";
    public string FunctionArguments { get; set; } = "{}";
}

/// <summary>
/// Agent 响应（可能包含文本内容和/或工具调用）
/// </summary>
public class AgentResponse
{
    public string? Content { get; set; }
    public List<ToolCall> ToolCalls { get; set; } = new();
    public string FinishReason { get; set; } = "";  // stop/tool_calls/error

    public bool HasToolCalls => ToolCalls.Count > 0;
}
