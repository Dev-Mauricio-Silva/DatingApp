namespace API.Interfaces;

public interface IUnityOfWork
{
    IUserRepository UserRepository {get;}
    IMessageRepository MessageRepository {get;}
    ILikesRepository LikesRepository {get;}
    Task<bool> Complete();
    bool HasChanges();
}
