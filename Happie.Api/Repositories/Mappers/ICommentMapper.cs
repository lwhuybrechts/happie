using Happie.Api.Repositories.Entities;
using Happie.Shared.Domain;

namespace Happie.Api.Repositories.Mappers;

/// <summary>Maps between <see cref="CommentEntity"/> and <see cref="Comment"/>.</summary>
public interface ICommentMapper
{
    /// <summary>Maps a <see cref="CommentEntity"/> to a <see cref="Comment"/> domain record.</summary>
    Comment ToModel(Guid householdId, CommentEntity entity);

    /// <summary>Maps a <see cref="Comment"/> domain record to a <see cref="CommentEntity"/>.</summary>
    CommentEntity ToEntity(Comment comment);
}
