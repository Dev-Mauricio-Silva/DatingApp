using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Helpers;
using API.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Authorize]
    public class MessagesController(IUnityOfWork unityOfWork, 
    IMapper mapper) : BaseApiController
    {
        [HttpPost]
        public async Task<ActionResult<MessageDto>> CreateMessage(CreateMessageDto  createMessageDto)
        {
            var username = User.GetUserName();

            if(username == createMessageDto.RecipientUsername.ToLower())    
                return BadRequest("You cannot message yourself");
            
            var sender = await unityOfWork.UserRepository.GetUserByUsernameAsync(username);
            var recipient = await unityOfWork.UserRepository.GetUserByUsernameAsync(createMessageDto.RecipientUsername);

            if(recipient == null || sender == null || sender.UserName == null || recipient.UserName == null) 
                return BadRequest("Cannot send message at this time");

            var message = new Message
            {
               Sender = sender,
               Recipient = recipient,
               SenderUsername = sender.UserName,
               RecipientUsername = recipient.UserName,
               Content = createMessageDto.Content
            };

            unityOfWork.MessageRepository.AddMessage(message);

            if(await unityOfWork.Complete()) return Ok(mapper.Map<MessageDto>(message));

            return BadRequest("Failed to save message.");

        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<MessageDto>>> GetMessagesForUser(
            [FromQuery] MessageParams messageParams)
        {
            messageParams.Username = User.GetUserName();

            var messages = await unityOfWork.MessageRepository.GetMessagesForUser(messageParams);

            Response.AddPaginationHeader(messages);

            return messages;
        }

        [HttpGet("thread/{username}")]
        public async Task<ActionResult<IEnumerable<MessageDto>>> GetMessageThread(string username)
        {
            var currentUsername = User.GetUserName();

            return Ok(await unityOfWork.MessageRepository.GetMessageThread(currentUsername, username));
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteMessage(int id)
        {
            var username = User.GetUserName();

            var message = await unityOfWork.MessageRepository.GetMessage(id);

            if(message == null) return BadRequest("Cannot delete this message");

            if(message.SenderUsername != username && message.RecipientUsername != username)
                return Forbid();
            
            if(message.SenderUsername == username) message.SenderDeleted = true;
            if(message.RecipientUsername == username) message.RecipientDeleted = true;

            if(message is {SenderDeleted: true, RecipientDeleted: true}){
                unityOfWork.MessageRepository.DeleteMessage(message);
            }

            if(await unityOfWork.Complete()) return Ok();

            return BadRequest("Failed to delete message");
        }
    }
}
