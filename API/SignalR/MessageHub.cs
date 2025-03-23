using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.SignalR;

namespace API.SignalR;

public class MessageHub(IUnityOfWork unityOfWork, 
IMapper mapper, IHubContext<PresenceHub> presenceHub) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var otherUser = httpContext?.Request.Query["user"];

        if(Context.User == null || string.IsNullOrEmpty(otherUser)) 
            throw new Exception("Cannot join group");
        var groupName = GetGroupName(Context.User.GetUserName(), otherUser);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        var group = await AddToGroup(groupName);

        await Clients.Group(groupName).SendAsync("UpdatedGroup", group);

        var messages = await unityOfWork.MessageRepository.GetMessageThread(Context.User.GetUserName(), otherUser!);

        if(unityOfWork.HasChanges()) await unityOfWork.Complete();

        await Clients.Caller.SendAsync("ReceiveMessageThread", messages);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var group = await RemoveFromMessageRoup();
        await Clients.Group(group.Name).SendAsync("UpdatedGroup", group);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendMessage(CreateMessageDto createMessageDto)
    {
        var username = Context.User?.GetUserName() ?? throw new Exception("Could not get user");

            if(username == createMessageDto.RecipientUsername.ToLower())    
                throw new HubException("You cannot message yourself");
            
            var sender = await unityOfWork.UserRepository.GetUserByUsernameAsync(username);
            var recipient = await unityOfWork.UserRepository.GetUserByUsernameAsync(createMessageDto.RecipientUsername);

            if(recipient == null || sender == null || sender.UserName == null || recipient.UserName == null) 
                throw new HubException("Cannot send message at this time");

            var message = new Message
            {
               Sender = sender,
               Recipient = recipient,
               SenderUsername = sender.UserName,
               RecipientUsername = recipient.UserName,
               Content = createMessageDto.Content
            };

            var groupName = GetGroupName(sender.UserName, recipient.UserName);
            var group = await unityOfWork.MessageRepository.GetMessageGroup(groupName);

            if(group != null && group.Connections.Any(x => x.Username == recipient.UserName))
            {
                message.DateRead = DateTime.UtcNow;
            }
            else
            {
                var connections = await PresenceTracker.GetConnectionsForUser(recipient.UserName);
                if(connections != null && connections?.Count != null)
                {
                    await presenceHub.Clients.Clients(connections).SendAsync("NewMessageReceived", 
                    new{username = sender.UserName, KnownAs = sender.KnownAs});
                }
            }

            unityOfWork.MessageRepository.AddMessage(message);

            if(await unityOfWork.Complete())
            {
                await Clients.Group(groupName).SendAsync("NewMessage", mapper.Map<MessageDto>(message));
            }
    }

    private async Task<Group> AddToGroup(string groupName)
    {
        var username = Context.User?.GetUserName() ?? throw new Exception("Could not get username");
        var group = await unityOfWork.MessageRepository.GetMessageGroup(groupName);
        var connection = new Connection{ ConnectionId = Context.ConnectionId, Username = username };

        if(group == null)
        {
            group = new Group{Name = groupName};
            unityOfWork.MessageRepository.AddGroup(group);
        }

        group.Connections.Add(connection);

        if(await unityOfWork.Complete()) return group;

        throw new HubException("Failed to join group");
    }

    private async Task<Group> RemoveFromMessageRoup()
    {
        var group = await unityOfWork.MessageRepository.GetGroupForConnection(Context.ConnectionId);
        var connection = group?.Connections.FirstOrDefault(x => x.ConnectionId == Context.ConnectionId);
        if(connection != null && group != null)
        {
            unityOfWork.MessageRepository.RemoveConnection(connection);
            if(await unityOfWork.Complete()) return group;
        }
        
        throw new Exception("Failed to  remove from group");
    }

    private string GetGroupName(string caller, string? other)
    {
        var stringCompare = string.CompareOrdinal(caller, other) < 0;
        return stringCompare ? $"{caller}-{other}" : $"{other}-{caller}";
    }
}
