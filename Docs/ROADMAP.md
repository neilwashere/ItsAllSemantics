### **Phase 1: Project Foundation & UI Scaffolding**

* [ ] **Project Setup:**
  * [x] Setup Tailwind from CDN CSS for the Blazor project.
  * [x] Clean up the default template pages and components.
* [ ] **Component Creation:**
Use the [WireFrame](./WireFrame.html) for guide
  * [x] Create a Shared/MainLayout.razor to define the core header/content/footer structure.
  * [x] Create a Components/Header.razor component for the title and menu.
  * [x] Create a Components/ChatInput.razor component for the message input and send button.
  * [x] Create a Components/ChatWindow.razor component to display the list of messages.
  * [x] Create a Components/ChatMessage.razor component to render a single message bubble (differentiating between user and AI).
* [ ] **Static UI Assembly:**
  * [x] Assemble the static components on the main Home.razor page.
  * [x] Create a C# model class for a message (e.g., ChatMessageModel with properties for Text, Author, Timestamp).
  * [x] Populate the Home.razor page with a hard-coded List<ChatMessageModel> to test the UI rendering.

### **Phase 2: Real-time Backend with SignalR**

* [ ] **SignalR Hub Setup:**
  * [x] Add the SignalR NuGet package to your project.
  * [x] Create a Hubs/ChatHub.cs class that inherits from Hub.
  * [x] Register the SignalR services and the ChatHub endpoint in Program.cs.
* [ ] **Semantic Kernel Integration:**
  * [ ] Add the Microsoft.SemanticKernel NuGet package.
  * [ ] In Program.cs, configure and register the Semantic Kernel service with your LLM provider (e.g., OpenAI).
  * [ ] Create a simple "Chat" plugin/skill for the kernel.
* [ ] **Hub Logic:**
  * [ ] Inject the Semantic Kernel into the ChatHub.
  * [x] Create a public async method in the hub, like public async Task SendMessage(string message).
  * [ ] Inside SendMessage, invoke the Semantic Kernel with the user's message to get a response.
  * [x] Use Clients.Caller.SendAsync("ReceiveMessage", ...) to send the user's original message back for display.
  * [x] Use Clients.Caller.SendAsync("ReceiveMessage", ...) again to send the AI's generated response.

### **Phase 3: Connecting Blazor Frontend to SignalR**

* [ ] **SignalR Client Setup:**
  * [x] In your Home.razor page, inject the NavigationManager.
  * [x] Add a HubConnection field in the @code block.
  * [x] In the OnInitializedAsync lifecycle method, build and start the HubConnection.
* [ ] **Sending & Receiving Messages:**
  * [x] Create a C# method to handle the form submission in ChatInput.razor. This will invoke the SendMessage method on the hubConnection.
  * [x] Register a handler for the "ReceiveMessage" event using hubConnection.On(...).
  * [x] The handler's callback will add the received message to the local List<ChatMessageModel> and call StateHasChanged() to update the UI.
* [ ] **UI/UX Refinements:**
  * [ ] Implement auto-scrolling to the bottom of the ChatWindow using JavaScript interop after a new message is added.
  * [x] Add a boolean flag (e.g., isWaitingForResponse) to show a "typing..." indicator in the UI.

### **Phase 4: Context & Deployment**

* [ ] **Conversation Context:**
  * [ ] Modify the ChatHub to maintain conversation history. For a simple implementation, you can pass the history from the client with each new message.
  * [ ] Update your Semantic Kernel plugin to accept and utilize the chat history for more contextually aware responses.
* [ ] **Error Handling:**
  * [ ] Add try-catch blocks around the hubConnection calls in the Blazor component.
  * [ ] Implement logic in the hub to handle potential errors from the Semantic Kernel API.
* [ ] **Deployment:**
  * [ ] Choose a hosting platform (Azure App Service is a natural fit).
  * [ ] Configure environment variables for your LLM API keys using the secrets manager or Azure Key Vault.
  * [ ] Publish the application.
  * [ ] If scaling, consider using the Azure SignalR Service to handle the WebSocket connections.
