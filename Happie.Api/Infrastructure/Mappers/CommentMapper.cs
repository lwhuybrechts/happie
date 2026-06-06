using Happie.Api.Infrastructure.Entities;
using Happie.Api.Domain;
using Happie.Shared.Domain;

namespace Happie.Api.Infrastructure.Mappers;

/// <summary>Maps between <see cref="CommentEntity"/> and <see cref="Comment"/>.</summary>
public class CommentMapper : ICommentMapper
{
    /// <inheritdoc/>
    public Comment ToModel(Guid householdId, CommentEntity entity)
    {
        // Row key format: "YYYY-MM-DD_HousemateId".
        var date = DateOnly.Parse(entity.RowKey[..10]);
        return new Comment(householdId, entity.HousemateId, date, entity.Text, entity.LastEditedAt == default ? null : entity.LastEditedAt, entity.LastModified == default ? null : entity.LastModified);
    }

    /// <inheritdoc/>
    public CommentEntity ToEntity(Comment comment)
    {
        var entity = new CommentEntity(comment.HouseholdId, comment.Date, comment.HousemateId);
        entity.HousemateId = comment.HousemateId;
        entity.Text = comment.Text;
        entity.LastEditedAt = comment.LastEditedAt ?? default;
        entity.LastModified = comment.LastModified ?? default;
        return entity;
    }
}
