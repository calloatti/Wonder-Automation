using Bindito.Core;
using System;
using Timberborn.Automation;
using Timberborn.AutomationUI;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.DuplicationSystem;
using Timberborn.EntityPanelSystem;
using Timberborn.Persistence;
using Timberborn.TemplateInstantiation;
using Timberborn.Wonders;
using Timberborn.WorldPersistence;
using UnityEngine.UIElements;

namespace Calloatti.WonderAutomation
{
  /// <summary>
  /// A custom terminal that generates a dedicated automation pin 
  /// specifically for launching the Wonder, separate from the building's pause pin.
  /// </summary>
  public class WonderLaunchTerminal : BaseComponent, IAwakableComponent, ITerminal, IPersistentEntity, IDuplicable<WonderLaunchTerminal>
  {
    private static readonly ComponentKey ComponentKey = new ComponentKey("WonderLaunchTerminal");
    private static readonly PropertyKey<Automator> LaunchInputKey = new PropertyKey<Automator>("LaunchInput");

    private readonly ReferenceSerializer _referenceSerializer;

    private Wonder _wonder;
    private Automator _automator;
    private AutomatorConnection _launchInput;
    private bool _previousState;

    // Exposed for the UI Fragment to read the current connection
    public Automator Input => _launchInput.Transmitter;

    // Event to notify the UI when the player changes the wire
    public event EventHandler InputReconnected;

    public WonderLaunchTerminal(ReferenceSerializer referenceSerializer)
    {
      _referenceSerializer = referenceSerializer;
    }

    public void Awake()
    {
      _wonder = GetComponent<Wonder>();
      _automator = GetComponent<Automator>();

      // Creates the physical second pin on the building
      _launchInput = _automator.AddInput();
    }

    /// <summary>
    /// Exposed for the UI Fragment to connect a new wire from the dropdown.
    /// </summary>
    public void SetInput(Automator automator)
    {
      if (automator != _launchInput.Transmitter)
      {
        _launchInput.Connect(automator);
        InputReconnected?.Invoke(this, EventArgs.Empty);
      }
    }

    public void Evaluate()
    {
      bool currentState = _launchInput.State == ConnectionState.On;

      // Trigger the wonder only on a rising edge (Off -> On)
      if (currentState && !_previousState)
      {
        if (_wonder.CanBeActivated())
        {
          _wonder.Activate();
        }
      }

      _previousState = currentState;
    }

    public void Save(IEntitySaver entitySaver)
    {
      if (_launchInput.IsConnected)
      {
        entitySaver.GetComponent(ComponentKey).Set(LaunchInputKey, _launchInput.Transmitter, _referenceSerializer.Of<Automator>());
      }
    }

    public void Load(IEntityLoader entityLoader)
    {
      if (entityLoader.TryGetComponent(ComponentKey, out var objectLoader) &&
          objectLoader.Has(LaunchInputKey) &&
          objectLoader.GetObsoletable(LaunchInputKey, _referenceSerializer.Of<Automator>(), out var value))
      {
        _launchInput.Connect(value);
      }
    }

    public void DuplicateFrom(WonderLaunchTerminal source)
    {
      _launchInput.Connect(source._launchInput.Transmitter);
    }
  }

  /// <summary>
  /// The UI Fragment that displays the Dropdown for the Launch pin.
  /// </summary>
  public class WonderLaunchTerminalFragment : IEntityPanelFragment
  {
    private readonly VisualElementLoader _visualElementLoader;
    private readonly TransmitterSelectorInitializer _transmitterSelectorInitializer;

    private VisualElement _root;
    private TransmitterSelector _inputSelector;
    private WonderLaunchTerminal _terminal;

    public WonderLaunchTerminalFragment(VisualElementLoader visualElementLoader, TransmitterSelectorInitializer transmitterSelectorInitializer)
    {
      _visualElementLoader = visualElementLoader;
      _transmitterSelectorInitializer = transmitterSelectorInitializer;
    }

    public VisualElement InitializeFragment()
    {
      // We can reuse the exact same UI visual tree that standard automation uses
      _root = _visualElementLoader.LoadVisualElement("Game/EntityPanel/AutomatableFragment");
      _inputSelector = _root.Q<TransmitterSelector>("Input");

      // Initialize it with our custom Getters and Setters
      _transmitterSelectorInitializer.InitializeStandalone(
          _inputSelector,
          () => _terminal.Input,
          automator => _terminal.SetInput(automator)
      );

      // Re-label the dropdown so players know this one is for the launch sequence
      Label label = _inputSelector.Q<Label>();
      if (label != null)
      {
        label.text = "Automate Launch";
      }

      _root.ToggleDisplayStyle(visible: false);
      return _root;
    }

    public void ShowFragment(BaseComponent entity)
    {
      if (entity.TryGetComponent<WonderLaunchTerminal>(out _terminal))
      {
        _inputSelector.Show(_terminal);
        _terminal.InputReconnected += OnInputReconnected;
        _root.ToggleDisplayStyle(visible: true);
      }
    }

    public void UpdateFragment()
    {
      if (_terminal != null)
      {
        _inputSelector.UpdateStateIcon();
      }
    }

    public void ClearFragment()
    {
      if (_terminal != null)
      {
        _terminal.InputReconnected -= OnInputReconnected;
        _terminal = null;
      }
      _inputSelector.ClearItems();
      _root.ToggleDisplayStyle(visible: false);
    }

    private void OnInputReconnected(object sender, EventArgs e)
    {
      _inputSelector.UpdateSelectedValue();
    }
  }

  /// <summary>
  /// Injects the custom terminal and the UI panel into the game.
  /// </summary>
  [Context("Game")]
  public class WonderAutomationConfigurator : Configurator
  {
    protected override void Configure()
    {
      Bind<WonderLaunchTerminal>().AsTransient();
      Bind<WonderLaunchTerminalFragment>().AsSingleton();

      MultiBind<TemplateModule>().ToProvider(ProvideTemplateModule).AsSingleton();
      MultiBind<EntityPanelModule>().ToProvider<EntityPanelModuleProvider>().AsSingleton();
    }

    private static TemplateModule ProvideTemplateModule()
    {
      TemplateModule.Builder builder = new TemplateModule.Builder();
      builder.AddDecorator<Wonder, WonderLaunchTerminal>();
      return builder.Build();
    }

    private class EntityPanelModuleProvider : IProvider<EntityPanelModule>
    {
      private readonly WonderLaunchTerminalFragment _fragment;

      public EntityPanelModuleProvider(WonderLaunchTerminalFragment fragment)
      {
        _fragment = fragment;
      }

      public EntityPanelModule Get()
      {
        EntityPanelModule.Builder builder = new EntityPanelModule.Builder();
        // Adds our custom UI dropdown to the bottom of the building menu
        builder.AddBottomFragment(_fragment);
        return builder.Build();
      }
    }
  }
}