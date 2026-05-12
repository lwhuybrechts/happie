using Happie.Api.Infrastructure.Entities;
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
        return new Comment(householdId, entity.HousemateId, date, entity.Text);
    }

    /// <inheritdoc/>
    public CommentEntity ToEntity(Comment comment)
    {
        var entity = new CommentEntity(comment.HouseholdId, comment.Date, comment.HousemateId);
        entity.HousemateId = comment.HousemateId;
        entity.Text = comment.Text;
        return entity;
    }
}
