using Bindito.Core;
using System;
using Timberborn.Automation;
using Timberborn.AutomationUI;
using Timberborn.BaseComponentSystem;
using Timberborn.CoreUI;
using Timberborn.DuplicationSystem;
using Timberborn.EntityPanelSystem;
using Timberborn.Localization;
using Timberborn.Persistence;
using Timberborn.SingletonSystem;
using Timberborn.TemplateInstantiation;
using Timberborn.Wonders;
using Timberborn.WorldPersistence;
using UnityEngine.UIElements;
using Timberborn.DropdownSystem;

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
    private static readonly string TransmitterSelectorNoneClass = "transmitter-selector--automatable-none";

    private readonly VisualElementLoader _visualElementLoader;
    private readonly AutomatorRegistry _automatorRegistry;
    private readonly EventBus _eventBus;
    private readonly ILoc _loc;
    private readonly DropdownItemsSetter _dropdownItemsSetter;
    private readonly AutomationStateIconBuilder _automationStateIconBuilder;
    private readonly TransmitterPickerTool _transmitterPickerTool;

    private VisualElement _root;
    private TransmitterSelector _inputSelector;
    private WonderLaunchTerminal _terminal;

    public WonderLaunchTerminalFragment(
        VisualElementLoader visualElementLoader,
        AutomatorRegistry automatorRegistry,
        EventBus eventBus,
        ILoc loc,
        DropdownItemsSetter dropdownItemsSetter,
        AutomationStateIconBuilder automationStateIconBuilder,
        TransmitterPickerTool transmitterPickerTool)
    {
      _visualElementLoader = visualElementLoader;
      _automatorRegistry = automatorRegistry;
      _eventBus = eventBus;
      _loc = loc;
      _dropdownItemsSetter = dropdownItemsSetter;
      _automationStateIconBuilder = automationStateIconBuilder;
      _transmitterPickerTool = transmitterPickerTool;
    }

    public VisualElement InitializeFragment()
    {
      _root = _visualElementLoader.LoadVisualElement("Game/EntityPanel/AutomatableFragment");
      _inputSelector = _root.Q<TransmitterSelector>("Input");

      // Set up our accessors
      Func<Automator> getter = () => _terminal.Input;
      Action<Automator> setter = (automator) => _terminal.SetInput(automator);

      // Create the custom Dropdown Provider to inject our specific Localization Keys
      TransmitterDropdownProvider transmitterDropdownProvider = new TransmitterDropdownProvider(
          _automatorRegistry,
          _loc,
          getter,
          setter,
          "Automation.AutomationNone",                         // Key for "None" when inside the dropdown list
          "Building.WonderLaunchTerminal.AutomateLaunch"       // Key for "Automate Launch" when empty and unselected
      );

      AutomationStateIcon automationStateIcon = _automationStateIconBuilder
          .Create(_inputSelector.Q<Image>("StateIcon"), getter)
          .SetClickableIcon()
          .Build();

      // Initialize the TransmitterSelector directly, bypassing the initializer wrapper
      _inputSelector.Initialize(
          _dropdownItemsSetter,
          _eventBus,
          _transmitterPickerTool,
          transmitterDropdownProvider,
          automationStateIcon,
          setter
      );

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
        // Dynamically applies the CSS class that strips the dropdown arrow and styles it as a button when empty
        _inputSelector.EnableInClassList(TransmitterSelectorNoneClass, _terminal.Input == null);
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