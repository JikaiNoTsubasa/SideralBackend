namespace dependancies.SharedClasses;

// Interface marqueur : les classes qui l'implémentent sont enregistrées automatiquement en "scoped" (durée de vie = une requête)
public interface IScoped
{
}

// Interface marqueur : les classes qui l'implémentent sont enregistrées automatiquement en "singleton" (durée de vie = application)
public interface ISingleton
{
}
