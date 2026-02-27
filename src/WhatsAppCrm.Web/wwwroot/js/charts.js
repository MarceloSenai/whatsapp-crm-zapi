// Chart.js interop for Blazor Dashboard
const _chartInstances = {};

function destroyChart(canvasId) {
    if (_chartInstances[canvasId]) {
        _chartInstances[canvasId].destroy();
        delete _chartInstances[canvasId];
    }
}

window.renderAreaChart = function (canvasId, labels, data) {
    destroyChart(canvasId);
    const ctx = document.getElementById(canvasId);
    if (!ctx) return;
    _chartInstances[canvasId] = new Chart(ctx, {
        type: 'line',
        data: {
            labels: labels,
            datasets: [{
                label: 'Leads',
                data: data,
                borderColor: '#25d366',
                backgroundColor: 'rgba(37, 211, 102, 0.15)',
                fill: true,
                tension: 0.4,
                borderWidth: 2,
                pointRadius: 2
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { display: false } },
            scales: {
                x: { grid: { display: false }, ticks: { font: { size: 10 } } },
                y: { beginAtZero: true, ticks: { stepSize: 1, font: { size: 10 } } }
            }
        }
    });
};

window.renderBarChart = function (canvasId, labels, spendData, revenueData) {
    destroyChart(canvasId);
    const ctx = document.getElementById(canvasId);
    if (!ctx) return;
    _chartInstances[canvasId] = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [
                {
                    label: 'Gasto',
                    data: spendData,
                    backgroundColor: '#ef4444',
                    borderRadius: 4
                },
                {
                    label: 'Receita',
                    data: revenueData,
                    backgroundColor: '#22c55e',
                    borderRadius: 4
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { position: 'top', labels: { font: { size: 11 } } } },
            scales: {
                x: { grid: { display: false }, ticks: { font: { size: 10 } } },
                y: {
                    ticks: {
                        font: { size: 10 },
                        callback: function(v) { return 'R$' + (v / 1000).toFixed(0) + 'k'; }
                    }
                }
            }
        }
    });
};

window.renderPieChart = function (canvasId, labels, data, colors) {
    destroyChart(canvasId);
    const ctx = document.getElementById(canvasId);
    if (!ctx) return;
    _chartInstances[canvasId] = new Chart(ctx, {
        type: 'pie',
        data: {
            labels: labels,
            datasets: [{
                data: data,
                backgroundColor: colors || [
                    '#6366f1', '#f59e0b', '#10b981', '#ef4444', '#8b5cf6',
                    '#06b6d4', '#f97316', '#84cc16', '#ec4899', '#14b8a6'
                ],
                borderWidth: 1
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { position: 'right', labels: { font: { size: 11 }, padding: 12 } }
            }
        }
    });
};

window.destroyChart = destroyChart;
