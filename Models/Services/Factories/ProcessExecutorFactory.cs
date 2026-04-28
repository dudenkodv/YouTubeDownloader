using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using YouTubeDownloader.Models.Entities;
using YouTubeDownloader.Models.Interfaces;
using YouTubeDownloader.Models.Interfaces.Factories;

namespace YouTubeDownloader.Models.Services.Factories;

public class ProcessExecutorFactory : IProcessExecutorFactory {
    private readonly IServiceProvider _serviceProvider;

    public ProcessExecutorFactory(IServiceProvider serviceProvider) {
        _serviceProvider = serviceProvider;
    }

    public IProcessExecutor Create(ProcessExecutorTypeEnum type) {
        return type switch {
            ProcessExecutorTypeEnum.CliWrap => _serviceProvider.GetRequiredService<ProcessExecutor>(),
            ProcessExecutorTypeEnum.Killable => _serviceProvider.GetRequiredService<ProcessKillExecutor>(),
            _ => throw new NotSupportedException($"Executor type {type} not supported")
        };
    }
}