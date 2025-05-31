using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;

public class LightsPlugin
{
  private readonly Dictionary<string, bool> _lightsStatus = new Dictionary<string, bool>();

  [KernelFunction, Description("Turn on the lights in a specific location")]
  public async Task<string> TurnOn([Description("The location of the lights to turn on")] string location)
  {
    await Task.Delay(500); // 0.5 second delay
    this._lightsStatus[location] = true;
    return $"Lights in {location} have been turned on.";
  }

  [KernelFunction, Description("Turn off the lights in a specific location")]
  public async Task<string> TurnOff([Description("The location of the lights to turn off")] string location)
  {
    await Task.Delay(500); // 0.5 second delay
    this._lightsStatus[location] = false;
    return $"Lights in {location} have been turned off.";
  }

  [KernelFunction, Description("Get the current status of the lights in a specific location")]
  public async Task<string> GetStatus([Description("The location of the lights to check")] string location)
  {
    await Task.Delay(500); // 0.5 second delay
    bool isOn = this._lightsStatus.GetValueOrDefault(location, false);
    return isOn ? $"The lights in {location} are currently on." : $"The lights in {location} are currently off.";
  }
}
