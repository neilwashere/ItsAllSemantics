### **Phase 1: Project Foundation & UI Scaffolding**

* [x] **Project Setup:**
  * [x] Setup Tailwind from CDN CSS for the Blazor project.
  * [x] Clean up the default template pages and components.
* [x] **Component Creation:**
Use the [WireFrame](./WireFrame.html) for guide
  * [x] Create a Shared/MainLayout.razor to define the core header/content/footer structure.
  * [x] Create a Components/Header.razor component for the title and menu.
  * [x] Create a Components/ChatInput.razor component for the message input and send button.
  * [x] Create a Components/ChatWindow.razor component to display the list of messages.
  * [x] Create a Components/ChatMessage.razor component to render a single message bubble (differentiating between user and AI).
* [x] **Static UI Assembly:**
  * [x] Assemble the static components on the main Home.razor page.
  * [x] Create a C# model class for a message (e.g., ChatMessageModel with properties for Text, Author, Timestamp).
  * [x] Populate the Home.razor page with a hard-coded List<ChatMessageModel> to test the UI rendering.

### **Phase 2: Real-time Backend with SignalR**

* [x] **SignalR Hub Setup:**
  * [x] Add the SignalR NuGet package to your project.
  * [x] Create a Hubs/ChatHub.cs class that inherits from Hub.
  * [x] Register the SignalR services and the ChatHub endpoint in Program.cs.
* [x] **Semantic Kernel Integration (Kernel-first):**
  * [x] Add Microsoft.SemanticKernel NuGet package.
  * [x] Configure Kernel (OpenAI/Azure OpenAI) via options + feature flag.
  * [x] Introduce `IChatResponder` abstraction (Echo + SemanticKernel implementations).
  * [x] Maintain per-session multi-turn history (ChatHistory) keyed by SignalR ConnectionId.
  * [x] Use `IChatCompletionService.GetStreamingChatMessageContentsAsync` for token streaming.
* [x] **Hub Logic:**
  * [ ] Inject the Semantic Kernel into the ChatHub.
  * [x] Create a public async method in the hub, like public async Task SendMessage(string message).
    * [x] Inside SendMessage, invoke responder streaming pipeline translating events to client.
  * [x] Use Clients.Caller.SendAsync("ReceiveMessage", ...) to send the user's original message back for display.
  * [x] Use Clients.Caller.SendAsync("ReceiveMessage", ...) again to send the AI's generated response.

### **Phase 3: Connecting Blazor Frontend to SignalR**

* [x] **SignalR Client Setup:**
  * [x] In your Home.razor page, inject the NavigationManager.
  * [x] Add a HubConnection field in the @code block.
  * [x] In the OnInitializedAsync lifecycle method, build and start the HubConnection.
* [x] **Sending & Receiving Messages:**
  * [x] Create a C# method to handle the form submission in ChatInput.razor. This will invoke the SendMessage method on the hubConnection.
  * [x] Register a handler for the "ReceiveMessage" event using hubConnection.On(...).
  * [x] The handler's callback will add the received message to the local List<ChatMessageModel> and call StateHasChanged() to update the UI.
* [x] **UI/UX Refinements:**
  * [x] Auto-scroll (JS interop helper + throttling during streaming).
  * [x] Typing indicator with safety timeout and automatic suppression on first non-user message.
  * [x] Streaming bubble with blinking caret appears immediately on Start event.
  * [x] Render batching tuned (MaxBufferedUnacknowledgedRenderBatches, SignalR StreamBufferCapacity) to improve perceived latency.

### **Phase 3.5: Streaming Event Model**
* [x] Define `StreamingChatEvent` (Start/Delta/End/Error) for transport-agnostic streaming.
* [x] Update responder interface to return `IAsyncEnumerable<StreamingChatEvent>`.
* [x] Echo responder emits synthetic streaming events.
* [x] Semantic Kernel responder maps token fragments to Delta events and final text to End.
* [x] Hub translates events to client SignalR methods: ReceiveStreamStart/Delta/End.
* [x] Client accumulates deltas into `streamingText` and materializes final message on End.

### **Phase 4: Context & Deployment**

* [x] **Conversation Context:**
  * [x] Server-side per-session ChatHistory retained across messages (multi-turn working).
  * [ ] Persist history store beyond memory (optional: distributed cache / DB) for reconnect/resume.
* [ ] **Error Handling:**
  * [x] Hub wraps streaming in try/catch and emits fallback End on failure.
  * [x] Centralize SK exceptions -> structured Error event with user-friendly message.
  * [x] Client cancellation ("Stop generating") to propagate CancellationToken.
* [ ] **Deployment:**
### **Phase 5: Enhancements & Polish (Planned)**
* [x] Introduce cancel (stop) button during streaming (client -> hub cancellation).
* [ ] Add agent name/avatar mapping (use `Agent` property from StreamingChatEvent).
* [ ] Adaptive scroll strategy: pause auto-scroll if user scrolls up.
* [ ] Persist conversation history (per user) using a backing store.
* [ ] Add markdown rendering for AI responses (with sanitization).
* [ ] Add basic tests for Echo responder and event translation.
* [ ] Add diagnostics toggle component (StreamingDiagnostics) only in Development.
* [ ] Improve accessibility (ARIA live region for streaming content).

### **Completed Summary**
Core chat, multi-turn context, and streaming event-driven pipeline are implemented. Remaining work centers on resilience (cancellation, persistence), richer rendering (markdown), and deployment hardening.
  * [ ] Choose a hosting platform (Azure App Service is a natural fit).
  * [ ] Configure environment variables for your LLM API keys using the secrets manager or Azure Key Vault.
  * [ ] Publish the application.
  * [ ] If scaling, consider using the Azure SignalR Service to handle the WebSocket connections.
