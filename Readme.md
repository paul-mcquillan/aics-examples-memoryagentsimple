# AI Console Agent Example

A minimal .NET console application demonstrating how to interact with a memory-enabled AI agent.

This example focuses on the core interaction loop, with everything stipped down to be a good HelloWorld style example:

* Load configuration
* Start an agent thread
* Send user input
* Receive responses
* Persist memory at the end of the session

---

## 🚀 Getting Started

### 1. Clone the repository

```bash
git clone https://github.com/YOUR_USERNAME/YOUR_REPO.git
cd YOUR_REPO
```

---

### 2. Configure your settings

Copy the appsettings.json file and name the copy exactly `appsettingsCLI.json'.

Populate the values with your Azure and AI Foundry details to run the logic using your tenant.

Example:

```json
{
  "AzureAd": {
    "TenantId": "{m365-tenantid}",
    "ClientId": "{m365-app-registration}",
    "ClientSecret": "{m365-app-registration-secret}"
  }
}
```

> ⚠️ The 'appsettingsCLI.json' file is specifically **ignored by Git** and should never be committed.

---

### 3. Run the application

```bash
dotnet run
```

---

## 💬 Usage

Type messages into the console and press Enter.

To end the conversation, type any of:

```
exit
end
end thread
all done
```

---

## 🧠 What This Example Shows

This project demonstrates a simple orchestration flow:

```
User Input → Agent → Reasoning → Memory → Response
```

Key components:

* `MemoryAgentSimple` → handles reasoning and interaction
* `MemoryService` → persists conversation memory
* `Program.cs` → minimal host application

---

## 🧼 Project Structure

```
/Program.cs
/AgentServices/
/Memory/
/Services/
/TokenHelper/
appsettingsCLI.json (ignored)
.gitignore
```

---

## 🔐 Security Notes

* Secrets are **not stored in the repository**
* If you accidentally commit a key, **rotate it immediately**
* Use `.gitignore` to exclude sensitive files

---

## 📝 Notes

* This is a **minimal example** intended for learning and experimentation
* Production implementations would typically include:

  * dependency injection
  * logging
  * error handling strategies
  * retry policies
  * structured configuration

---

## 📚 Related Blog Post

👉 https://medium.com/@the_real_quill/building-a-simple-memory-agent-7ce4227d7017

---

## 🪪 License

This project is provided for educational purposes and is free to use.

Consider adding an MIT license if you plan to reuse this in other projects.

## Why this exists

Most AI examples are overly complex.

This project shows the smallest possible way to:
- run an agent
- maintain memory
- interact via console