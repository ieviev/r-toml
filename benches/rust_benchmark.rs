use std::time::Duration;

use criterion::{Criterion, Throughput, criterion_group, criterion_main};
use r_toml::Token;

fn one_test(c: &mut Criterion) {
    let mut builder = String::with_capacity(2_097_152);

    for i in (0..100_000).step_by(6) {
        builder.push_str(&format!("key{} = true\n", i));
        builder.push_str(&format!("key{} = false\n", i + 1));
        builder.push_str(&format!("key{} = 'asd'\n", i + 2));
        builder.push_str(&format!("key{} = 100\n", i + 3));
        builder.push_str(&format!("key{} = 0.01\n", i + 4));
        builder.push_str(&format!("key{} = '''tri'''\n", i + 5));
    }

    let data = builder;

    let mut toml_group = c.benchmark_group("r");
    toml_group.measurement_time(Duration::from_secs(15));
    toml_group.throughput(Throughput::Bytes(data.len() as u64));

    toml_group.bench_with_input("toml::from_str", &data, |b, data| {
        b.iter(|| {
            let _: toml::Value = toml::from_str(&data).expect("Failed to parse TOML");
        });
    });
    toml_group.bench_with_input("r_toml::to_vec", &data, |b, data| {
        b.iter(|| {
            let _ = r_toml::to_vec(data.as_bytes()).expect("Failed to parse TOML");
        });
    });
    toml_group.bench_with_input("r_toml::stream", &data, |b, data| {
        b.iter(|| {
            let mut count = 0;
            r_toml::stream(data.as_bytes(), |_, v| {
                if v.kind == Token::TRUE {
                    count += 1;
                }
            });
        });
    });
    toml_group.bench_with_input("r_toml::to_map", &data, |b, data| {
        b.iter(|| {
            let _ = r_toml::to_map(data.as_bytes()).expect("Failed to parse TOML");
        });
    });

    toml_group.finish();
}

criterion_group!(benches, one_test);
criterion_main!(benches);
