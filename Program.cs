using System.Diagnostics;
using EFCoreIssue.UpdateJSONCollectionProperty;
using Microsoft.EntityFrameworkCore;

var ctx = new AppDbContext();

await ctx.Database.MigrateAsync().ConfigureAwait(false);

var model = await ctx.Models.AddAsync(new Model()
{
    Name = "Model 1",
    JsonArray = [
        new() {
            Id = 1,
            Name = "Name 1",
        },
        new() {
            Id = 2,
            Name = "Name 2",
        }
    ]
});

await ctx.SaveChangesAsync().ConfigureAwait(false);

// simulate a API POST where a new model instance with the same ID is supplied
model.State = EntityState.Detached;
var updatedModel = new Model()
{
    Name = model.Entity.Name,
    Id = model.Entity.Id,
    JsonArray = model.Entity.JsonArray.Select(a => new JsonArrayItem()
    {
        Id = a.Id,
        Name = $"NEW {a.Name}"
    }).ToArray()
};

var updatedEntry = ctx.Update(updatedModel);

try {
    await ctx.SaveChangesAsync().ConfigureAwait(false);
} catch (InvalidOperationException ex) when (ex.HResult == -2146233079)
{
    // System.InvalidOperationException: The value of shadow key property 'JsonArrayItem.__synthesizedOrdinal' is unknown when attempting to save changes.
    // This is because shadow property values cannot be preserved when the entity is not being tracked.
    // Consider adding the property to the entity's .NET type. See https://aka.ms/efcore-docs-owned-collections for more information
}

// trying the suggestion from https://github.com/dotnet/efcore/issues/34616

updatedEntry.State = EntityState.Detached;

var arrayEntry = updatedEntry.Collection(e => e.JsonArray);

var i = 1;
foreach (var arrayItem in updatedModel.JsonArray)
{
    var itemEntry = arrayEntry.FindEntry(arrayItem)!;
    itemEntry.Property("__synthesizedOrdinal").CurrentValue = i++;
    // itemEntry.State = EntityState.Modified;
}

updatedEntry.State = EntityState.Modified;
var modified = await ctx.SaveChangesAsync().ConfigureAwait(false);

// ef log shows the json array was NOT updated, only the name was modified
Debug.Assert(modified == 1);

// try a modified solution, where we set the individual array item as modified...
updatedEntry.State = EntityState.Detached;

arrayEntry = updatedEntry.Collection(e => e.JsonArray);

i = 1;
foreach (var arrayItem in updatedModel.JsonArray)
{
    var itemEntry = arrayEntry.FindEntry(arrayItem)!;
    itemEntry.Property("__synthesizedOrdinal").CurrentValue = i++;

    // required for some reason??
    itemEntry.Property("ModelId").CurrentValue = 1;

    itemEntry.State = EntityState.Modified;
}

// would throw The property 'JsonArrayItem.ModelId' is part of a key and so cannot be modified or marked as modified.
// To change the principal of an existing entity with an identifying foreign key, first delete the dependent and invoke 'SaveChanges',
// and then associate the dependent with the new principal.
// updatedEntry.State = EntityState.Modified;
modified = await ctx.SaveChangesAsync().ConfigureAwait(false);

// nothing is updated
Debug.Assert(modified == 1);
